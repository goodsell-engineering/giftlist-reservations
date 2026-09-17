using Reservations.Application.Common;
using Reservations.Application.GiftLists;
using Reservations.Application.GiftLists.RecordGiftItemAdded;
using Reservations.Application.GiftLists.RecordGiftItemRemoved;
using Reservations.Application.GiftLists.RecordGiftListCreated;
using Reservations.Application.GiftLists.RecordGiftListDeleted;
using Reservations.Application.Reservations;
using Reservations.Infrastructure.GiftLists.Messaging;
using Reservations.Infrastructure.GiftLists.Persistence;
using Reservations.Infrastructure.Reservations.Persistence;
using GiftLists.Contracts.GiftLists.Events;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Rebus.Bus;
using Rebus.Config;

namespace Reservations.Infrastructure.Platform;

/// <summary>
/// Reservations' composition root, called once from <c>Reservations.Host</c>'s <c>Program.cs</c>.
/// Host itself contains no wiring beyond the call to this method (CONVENTIONS.md "Project
/// reference graph"); everything below is grouped by domain (<c>Reservations/...</c>,
/// <c>GiftLists/...</c>) rather than by technical category, same as production code
/// (CONVENTIONS.md "Folder structure") — <c>Platform/</c> holds only this aggregator, which
/// belongs to no single domain.
///
/// GL-34 adds the read side of "what is reservable": the <c>GiftListProjection</c> built from
/// GiftLists' own integration events, and the four Rebus handlers/interactors/validators that
/// keep it up to date. GL-35's own aggregate/repository/index are unchanged. No
/// <c>ReserveGift</c> use case exists yet (GL-36) — nothing here sends a command or publishes an
/// integration event of this service's own.
/// </summary>
public static class ReservationsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddReservationsInfrastructure(this IServiceCollection services)
    {
        AddReservations(services);
        AddGiftListProjection(services);
        return services;
    }

    /// <summary>
    /// Applies startup-time infrastructure that needs a live connection — today, just the unique
    /// (listId, itemId) index (ARCHITECTURE.md "Data model"; GL-35). The gift-list projection
    /// collection needs no equivalent call: every query it serves
    /// (<see cref="IGiftListProjectionRepository.FindByIdAsync"/>) filters on <c>_id</c>, which
    /// Mongo already indexes by default. Called once from <c>Program.cs</c> after the host is
    /// built, mirroring how Mongo/Rebus health checks are wired ahead of any use case.
    /// </summary>
    public static Task EnsureIndexesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var database = serviceProvider.GetRequiredService<IMongoDatabase>();
        return ReservationRepository.EnsureIndexesAsync(database, cancellationToken);
    }

    /// <summary>
    /// Subscribes to the GiftLists integration events this projection is built from. Called once
    /// from <c>Program.cs</c>, after the host is built — mirrors <see cref="EnsureIndexesAsync"/>'s
    /// own placement/rationale, and <c>Gateway.Infrastructure.Platform.GatewayInfrastructureServiceCollectionExtensions.SubscribeToGiftListsEventsAsync</c>'s
    /// own shape exactly.
    ///
    /// Deliberately four subscriptions, not five: <c>GiftListRenamedV1</c> is not one of them.
    /// This projection carries no field a rename would ever update — no name is stored here at
    /// all (<c>GiftListProjection</c>'s own doc comment) — so subscribing to it would receive an
    /// event with nothing for any handler to do. <c>GiftListExpiredV1</c> is not one of them
    /// either, for a different reason: GiftLists does not publish it yet (ARCHITECTURE.md
    /// "Sagas: list expiry" describes the saga that will; it has not been built), and this
    /// service is built specifically not to depend on it ever arriving — GL-34's whole point
    /// (ARCHITECTURE.md "Auth & sharing": "Reservation checks expiresAt on its own
    /// projection rather than waiting for GiftListExpired to arrive").
    /// </summary>
    public static async Task SubscribeToGiftListsEventsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var bus = serviceProvider.GetRequiredService<IBus>();
        await bus.Subscribe<GiftListCreatedV1>();
        await bus.Subscribe<GiftListDeletedV1>();
        await bus.Subscribe<GiftItemAddedV1>();
        await bus.Subscribe<GiftItemRemovedV1>();
    }

    private static void AddReservations(IServiceCollection services)
    {
        services.AddScoped<IReservationRepository, ReservationRepository>();
    }

    private static void AddGiftListProjection(IServiceCollection services)
    {
        services.AddScoped<IGiftListProjectionRepository, GiftListProjectionRepository>();

        services.AddScoped<IValidator<RecordGiftListCreatedRequest>, RecordGiftListCreatedValidator>();
        services.AddScoped<IInteractor<RecordGiftListCreatedRequest, RecordGiftListCreatedResponse>, RecordGiftListCreatedInteractor>();

        services.AddScoped<IValidator<RecordGiftListDeletedRequest>, RecordGiftListDeletedValidator>();
        services.AddScoped<IInteractor<RecordGiftListDeletedRequest, RecordGiftListDeletedResponse>, RecordGiftListDeletedInteractor>();

        services.AddScoped<IValidator<RecordGiftItemAddedRequest>, RecordGiftItemAddedValidator>();
        services.AddScoped<IInteractor<RecordGiftItemAddedRequest, RecordGiftItemAddedResponse>, RecordGiftItemAddedInteractor>();

        services.AddScoped<IValidator<RecordGiftItemRemovedRequest>, RecordGiftItemRemovedValidator>();
        services.AddScoped<IInteractor<RecordGiftItemRemovedRequest, RecordGiftItemRemovedResponse>, RecordGiftItemRemovedInteractor>();

        // One open-generic decorator pair, applied to every IInteractor<,> registered above —
        // Validation, then Logging, in that order in every service (CONVENTIONS.md "Use cases").
        services.Decorate(typeof(IInteractor<,>), typeof(Validating<,>));
        services.Decorate(typeof(IInteractor<,>), typeof(Logging<,>));

        services.AddRebusHandler<GiftListCreatedV1Handler>();
        services.AddRebusHandler<GiftListDeletedV1Handler>();
        services.AddRebusHandler<GiftItemAddedV1Handler>();
        services.AddRebusHandler<GiftItemRemovedV1Handler>();
    }
}
