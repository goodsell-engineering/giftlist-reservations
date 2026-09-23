using BuildingBlocks.HealthChecks;
using BuildingBlocks.Logging;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Reservations.Infrastructure.Platform;

var builder = WebApplication.CreateBuilder(args);

// GL-45: scope rendering for the console provider WebApplication.CreateBuilder already
// registers — without this, CorrelationIdIncomingStep's own logger scope pushes correctly but
// silently, since Microsoft.Extensions.Logging's simple console formatter defaults
// IncludeScopes to false.
builder.Services.AddBuildingBlocksLogging();

// Reservations' own database/queue (ARCHITECTURE.md "Data model", "Tech stack") — wiring these here, ahead of any
// use case, is what makes "healthy Rebus connection + Mongo connection" (Phase 0 exit
// criterion) an observable fact rather than something scraped out of logs.
builder.Services.AddBuildingBlocksMongo(builder.Configuration, "reservation");
builder.Services.AddBuildingBlocksRebus(builder.Configuration, "reservation");
builder.Services.AddBuildingBlocksHealthChecks(builder.Configuration);

// GL-35: the Reservation aggregate's persistence shape and the unique (listId, itemId) index
// behind "first reserver wins". GL-34: the gift-list projection built from GiftLists' own
// integration events, and the Rebus handlers that keep it current. GL-36: the ReserveGift use
// case itself, reached over the same request/reply bridge Login/SignUp use (ARCHITECTURE.md
// "Command → event flow"), and the publisher that turns its GiftReserved domain event into
// GiftReservedV1 — everything below this line is Reservations' own composition root, in
// Reservations.Infrastructure.
builder.Services.AddReservationsInfrastructure();

var app = builder.Build();

// The unique (listId, itemId) index (ARCHITECTURE.md "Data model"; GL-35) is a correctness
// requirement, not an optimisation — applied once at startup rather than left to be inferred
// from application code.
await ReservationsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync(app.Services, CancellationToken.None);

// GL-34: the subscriptions that let GiftLists' integration events actually reach this process —
// a correctness requirement, applied once at startup rather than left implicit (mirrors
// Gateway.Host's own SubscribeToGiftListsEventsAsync call).
await ReservationsInfrastructureServiceCollectionExtensions.SubscribeToGiftListsEventsAsync(app.Services, CancellationToken.None);

// Liveness: only "is the process up and answering HTTP". Deliberately checks nothing
// external — a RabbitMQ/Mongo blip must not make Docker kill an otherwise-healthy container
// (ARCHITECTURE.md "Why workers still need a little HTTP").
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: the Mongo + RabbitMQ checks BuildingBlocks registered above, tagged "ready".
// This is the endpoint compose's healthcheck targets, because it's the one that should gate
// `depends_on: condition: service_healthy` for anything waiting on Reservations.
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();
