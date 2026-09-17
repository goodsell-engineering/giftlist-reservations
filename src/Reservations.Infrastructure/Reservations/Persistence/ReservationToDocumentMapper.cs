using Reservations.Domain.Reservations;

namespace Reservations.Infrastructure.Reservations.Persistence;

/// <summary>Source + "To" + target (CONVENTIONS.md "Naming").</summary>
internal static class ReservationToDocumentMapper
{
    public static ReservationDocument ToDocument(Reservation reservation) => new()
    {
        Id = reservation.Id.Value,
        ListId = reservation.ListId.Value,
        ItemId = reservation.ItemId.Value,
        // ReservationDocument's own doc comment explains why this is DateTime, not the
        // aggregate's DateTimeOffset — always UTC already, so .UtcDateTime is lossless.
        ReservedAt = reservation.ReservedAt.UtcDateTime,
        ReleaseSecret = reservation.ReleaseSecret.Value,
    };

    public static Reservation ToAggregate(ReservationDocument document) => Reservation.Rehydrate(
        new ReservationId(document.Id),
        new GiftListId(document.ListId),
        new GiftItemId(document.ItemId),
        new ReleaseSecret(document.ReleaseSecret),
        new DateTimeOffset(document.ReservedAt, TimeSpan.Zero));
}
