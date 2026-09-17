using BuildingBlocks.Results;
using Reservations.Application.Reservations;
using Reservations.Domain.Reservations;
using MongoDB.Driver;

namespace Reservations.Infrastructure.Reservations.Persistence;

internal sealed class ReservationRepository : IReservationRepository
{
    public const string CollectionName = "reservations";

    private readonly IMongoCollection<ReservationDocument> _reservations;

    public ReservationRepository(IMongoDatabase database)
    {
        _reservations = database.GetCollection<ReservationDocument>(CollectionName);
    }

    public async Task<Reservation?> FindByIdAsync(ReservationId id, CancellationToken cancellationToken)
    {
        var document = await _reservations
            .Find(d => d.Id == id.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ReservationToDocumentMapper.ToAggregate(document);
    }

    public async Task<Result> AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var document = ReservationToDocumentMapper.ToDocument(reservation);

        try
        {
            await _reservations.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Expected outcome, not a bug (CONVENTIONS.md "Errors") — the unique compound index
            // on (listId, itemId) below caught it. This IS "first reserver wins" (GL-35's whole
            // point, not GL-36's reply pattern): the index rejects every insert but the first
            // one Mongo itself accepts, so which caller "wins" a race is decided at the storage
            // layer, atomically, for every concurrent writer at once — not by whichever process
            // happened to check first. GL-36's ReserveGift interactor is what turns this Result
            // into the reply a client sees; this port only reports the outcome.
            return ReservationErrors.AlreadyReserved;
        }

        return Result.Success();
    }

    /// <summary>
    /// The uniqueness guarantee behind "first reserver wins" (GL-35; ARCHITECTURE.md "Data
    /// model") — a correctness requirement, not an optimisation, and the reason this issue exists
    /// at all: it must be applied and proven against a real MongoDB (via Testcontainers) before
    /// any handler is written on top of it. Applied at startup by
    /// <c>ReservationsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync</c>, not left
    /// to be inferred from application code (CONVENTIONS.md "Persistence").
    /// </summary>
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<ReservationDocument>(CollectionName);
        var listAndItemIndex = new CreateIndexModel<ReservationDocument>(
            Builders<ReservationDocument>.IndexKeys.Ascending(d => d.ListId).Ascending(d => d.ItemId),
            new CreateIndexOptions { Unique = true, Name = "listId_itemId_unique" });

        return collection.Indexes.CreateOneAsync(listAndItemIndex, cancellationToken: cancellationToken);
    }
}
