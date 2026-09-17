using BuildingBlocks.Messaging;
using BuildingBlocks.Testing;
using GiftLists.Contracts.GiftLists.Events;
using Reservations.Application.GiftLists;
using Reservations.Contracts.Reservations;
using Reservations.Contracts.Reservations.Events;
using Reservations.Domain.Reservations;
using Reservations.IntegrationTests.Fixtures;
using Reservations.IntegrationTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Config;

namespace Reservations.IntegrationTests.Reservations;

/// <summary>
/// <see cref="GiftReservedV1"/> landing on a real broker under its expected type name — the only
/// way to prove Rebus's own type-name-based routing/deserialization (CONVENTIONS.md "Testing")
/// still lines up with what a subscriber (the Gateway, in production) actually asks to subscribe
/// to. An in-memory transport would hand the object straight to a handler and never touch this.
/// Mirrors <c>Identity.IntegrationTests.Users.UserEventPublishingTests</c> exactly.
///
/// <see cref="GiftReservedV1Tests"/> (Reservations.UnitTests) already pins this event's SHAPE —
/// exactly three fields, never a reservation id or a release secret — by reflection alone; this
/// suite is only responsible for proving the real publish/subscribe path still works, not for
/// re-deriving that shape guarantee against a slower, flakier real broker.
/// </summary>
[Collection(ReservationsCollection.Name)]
public sealed class GiftReservedV1PublishingTests(ReservationsFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReserveGift_ShouldPublishGiftReservedV1_UnderItsOwnTypeName_ToARealSubscriber_WithNoReserverIdentity()
    {
        // Arrange — a subscriber wired up exactly the way the Gateway would be: it asks Rebus to
        // subscribe to GiftReservedV1 by .NET type, and only receives anything at all if the
        // publisher's wire type name still matches what the subscription was registered under.
        var (subscriber, queueName, capture) = await StartSubscriberAsync();
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        try
        {
            await fixture.GiftListsBus.Publish(new GiftListCreatedV1(
                listId, Guid.NewGuid(), "Birthday Wishlist", DateTimeOffset.UtcNow.AddDays(7), "ShareTokenPlaceholder", DateTimeOffset.UtcNow));
            await fixture.GiftListsBus.Publish(new GiftItemAddedV1(listId, itemId, "Lego Set", null, null, DateTimeOffset.UtcNow));
            await WaitForItemOnProjectionAsync(listId, itemId);

            // Act
            var reserved = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(new ReserveGift(listId, itemId));
            Assert.True(reserved.IsSuccess);
            var (received, typeHeader) = await capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

            // Assert
            Assert.Equal(listId, received.ListId);
            Assert.Equal(itemId, received.ItemId);
            Assert.NotNull(typeHeader);
            Assert.Contains(nameof(GiftReservedV1), typeHeader, StringComparison.Ordinal);
        }
        finally
        {
            // GL-57 review: on a .WithReuse(true) container, a queue and its topic binding left
            // behind here would accumulate across every local run and keep routing future
            // GiftReservedV1 events into a queue nobody drains.
            await subscriber.Services.GetRequiredService<IBus>().Unsubscribe<GiftReservedV1>();
            await subscriber.StopAsync();
            subscriber.Dispose();
            await QueueCleanup.DeleteAsync(fixture.RabbitMqConnectionString, queueName);
        }
    }

    /// <summary>
    /// Waits for this service's own gift-list projection to reflect the item just added, before
    /// sending <see cref="ReserveGift"/> — GL-34's handler and this test's own publish both race
    /// over the same real broker, so without this wait the reserve can (and does) arrive before
    /// the projection exists at all.
    /// </summary>
    private async Task WaitForItemOnProjectionAsync(Guid listId, Guid itemId)
    {
        using var scope = fixture.CreateReservationsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListProjectionRepository>();
        await Eventually.Async(
            () => repository.FindByIdAsync(new GiftListId(listId), CancellationToken.None),
            projection => projection?.ItemIds.Contains(new GiftItemId(itemId)) == true,
            TimeSpan.FromSeconds(15));
    }

    private async Task<(IHost Host, string QueueName, EventCapture<GiftReservedV1> Capture)> StartSubscriberAsync()
    {
        var capture = new EventCapture<GiftReservedV1>();
        var queueName = $"reservation-tests-sub.{Guid.NewGuid():N}";
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [RebusConfigurationExtensions.ConnectionStringConfigKey] = fixture.RabbitMqConnectionString,
        });
        builder.Services.AddSingleton(capture);
        builder.Services.AddBuildingBlocksRebus(builder.Configuration, queueName);
        builder.Services.AddRebusHandler<EventCapturingHandler<GiftReservedV1>>();
        var host = builder.Build();
        await host.StartAsync();
        await host.Services.GetRequiredService<IBus>().Subscribe<GiftReservedV1>();
        return (host, queueName, capture);
    }
}
