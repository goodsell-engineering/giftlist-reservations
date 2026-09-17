using Reservations.Application.Reservations;
using Reservations.Infrastructure.Reservations.Persistence;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Reservations.Infrastructure.Platform;

/// <summary>
/// Reservations' composition root, called once from <c>Reservations.Host</c>'s <c>Program.cs</c>.
/// Host itself contains no wiring beyond the call to this method (CONVENTIONS.md "Project
/// reference graph"); everything below is grouped by domain (<c>Reservations/...</c>) rather than
/// by technical category, same as production code (CONVENTIONS.md "Folder structure") —
/// <c>Platform/</c> holds only this aggregator, which belongs to no single domain.
///
/// GL-35's scope stops at the repository and its index: no interactor, no Rebus handler, and so
/// nothing here registers <c>IInteractor&lt;,&gt;</c> or an <c>AddRebusHandler&lt;&gt;</c> call —
/// that is GL-36's <c>ReserveGift</c> use case, added to this method when it exists.
/// </summary>
public static class ReservationsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddReservationsInfrastructure(this IServiceCollection services)
    {
        AddReservations(services);
        return services;
    }

    /// <summary>
    /// Applies startup-time infrastructure that needs a live connection — today, just the unique
    /// (listId, itemId) index (ARCHITECTURE.md "Data model"; GL-35). Called once from
    /// <c>Program.cs</c> after the host is built, mirroring how Mongo/Rebus health checks are
    /// wired ahead of any use case.
    /// </summary>
    public static Task EnsureIndexesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var database = serviceProvider.GetRequiredService<IMongoDatabase>();
        return ReservationRepository.EnsureIndexesAsync(database, cancellationToken);
    }

    private static void AddReservations(IServiceCollection services)
    {
        services.AddScoped<IReservationRepository, ReservationRepository>();
    }
}
