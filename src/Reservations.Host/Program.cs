using BuildingBlocks.HealthChecks;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Reservations.Infrastructure.Platform;

var builder = WebApplication.CreateBuilder(args);

// Reservations' own database/queue (ARCHITECTURE.md "Data model", "Tech stack") — wiring these here, ahead of any
// use case, is what makes "healthy Rebus connection + Mongo connection" (Phase 0 exit
// criterion) an observable fact rather than something scraped out of logs.
builder.Services.AddBuildingBlocksMongo(builder.Configuration, "reservation");
builder.Services.AddBuildingBlocksRebus(builder.Configuration, "reservation");
builder.Services.AddBuildingBlocksHealthChecks(builder.Configuration);

// GL-35: the Reservation aggregate's persistence shape and the unique (listId, itemId) index
// behind "first reserver wins" — everything below this line is Reservations' own composition
// root, in Reservations.Infrastructure. No interactor and no Rebus handler exist yet (GL-36 owns
// the ReserveGift use case), so there is nothing else for Host to wire in front of it.
builder.Services.AddReservationsInfrastructure();

var app = builder.Build();

// The unique (listId, itemId) index (ARCHITECTURE.md "Data model"; GL-35) is a correctness
// requirement, not an optimisation — applied once at startup rather than left to be inferred
// from application code.
await ReservationsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync(app.Services, CancellationToken.None);

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
