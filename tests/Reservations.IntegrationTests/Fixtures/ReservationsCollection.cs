namespace Reservations.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class ReservationsCollection : ICollectionFixture<ReservationsFixture>
{
    public const string Name = "Reservations";
}
