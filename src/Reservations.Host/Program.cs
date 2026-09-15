using BuildingBlocks.HealthChecks;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Reservations' own database/queue (ARCHITECTURE.md "Data model", "Tech stack") — wiring these here, ahead of any
// use case, is what makes "healthy Rebus connection + Mongo connection" (Phase 0 exit
// criterion) an observable fact rather than something scraped out of logs.
builder.Services.AddBuildingBlocksMongo(builder.Configuration, "reservation");
builder.Services.AddBuildingBlocksRebus(builder.Configuration, "reservation");
builder.Services.AddBuildingBlocksHealthChecks(builder.Configuration);

var app = builder.Build();

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
