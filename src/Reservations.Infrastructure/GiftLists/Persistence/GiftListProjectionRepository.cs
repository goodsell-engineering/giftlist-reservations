using BuildingBlocks.Persistence;
using Reservations.Application.GiftLists;
using Reservations.Application.GiftLists.RecordGiftItemAdded;
using Reservations.Application.GiftLists.RecordGiftItemRemoved;
using Reservations.Application.GiftLists.RecordGiftListCreated;
using Reservations.Application.GiftLists.RecordGiftListDeleted;
using MongoDB.Driver;

namespace Reservations.Infrastructure.GiftLists.Persistence;

/// <summary>
/// Builds <c>reservation.giftListProjections</c> from GiftLists' integration events. Every
/// <c>Apply*</c> method is a read-mutate-versioned-replace retry loop, mirroring
/// <c>Gateway.Infrastructure.GiftLists.Persistence.GiftListProjectionRepository</c>'s own — read
/// the current document (or none), compute what should change, and only write if something
/// actually would. "Nothing would change" covers both plain redelivery of the same event and a
/// genuinely stale, reordered one (an add older than an already-applied remove for the same item)
/// — <see cref="IGiftListProjectionRepository"/>'s own doc comment has the full reasoning.
///
/// The retry loop is bounded (<see cref="BoundedCasRetryPolicy.MaxAttempts"/>) rather than an
/// unbounded <c>while (true)</c> — a sustained, pathological burst of concurrent writers to one
/// list fails loudly with <see cref="GiftListProjectionApplyExhaustedException"/> instead of
/// spinning forever. See <see cref="_retryPolicy"/>'s own comment for why this service's own
/// attempt cap is re-derived rather than assumed equal to Gateway's, per
/// <see cref="BoundedCasRetryPolicy"/>'s own remarks.
/// </summary>
internal sealed class GiftListProjectionRepository : IGiftListProjectionRepository
{
    public const string CollectionName = "giftListProjections";

    /// <summary>
    /// This service's own re-derivation of <see cref="BoundedCasRetryPolicy.MaxAttempts"/>
    /// (<see cref="BoundedCasRetryPolicy"/>'s own remarks: "Reservations' own author overrides it,
    /// deliberately, at the call site — no edit to this file required"), not an inherited
    /// assumption that Gateway's topology applies here too. It lands on the same number, 8, for
    /// the same underlying reasons: Rebus's default <c>MaxParallelism</c> is 5 (unset by
    /// <c>RebusConfigurationExtensions</c>, same as every other service in this project), this
    /// repository has four <c>Apply*</c> handler types racing the SAME document under normal
    /// load, so 5 concurrent writers to one list is ordinary contention, not a burst; in the
    /// worst legitimate interleaving the unluckiest of those 5 loses to each of the other 4 in
    /// turn — 4 retries, 5 attempts total — plus one attempt of headroom for the
    /// insert-duplicate-key fallback path (a stub race that consumes an attempt without advancing
    /// the version, see <see cref="TryApplyOnceAsync"/>'s own comment) and two for jitter/
    /// scheduling, giving 8. A different topology here (a different queue's
    /// <c>MaxParallelism</c>, a different handler count) would call for a different number; this
    /// one does not.
    /// </summary>
    private readonly BoundedCasRetryPolicy _retryPolicy = new(maxAttempts: 8);

    private readonly IMongoCollection<GiftListProjectionDocument> _giftListProjections;

    public GiftListProjectionRepository(IMongoDatabase database)
    {
        _giftListProjections = database.GetCollection<GiftListProjectionDocument>(CollectionName);
    }

    public async Task<GiftListProjection?> FindByIdAsync(Guid listId, CancellationToken cancellationToken)
    {
        var document = await _giftListProjections
            .Find(d => d.Id == listId && d.HasCreated)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : GiftListProjectionDocumentMapper.ToProjection(document);
    }

    public Task ApplyListCreatedAsync(RecordGiftListCreatedRequest request, CancellationToken cancellationToken) =>
        ApplyAsync(request.ListId, existing => MutateOnCreated(existing, request), cancellationToken);

    public Task ApplyListDeletedAsync(RecordGiftListDeletedRequest request, CancellationToken cancellationToken) =>
        ApplyAsync(request.ListId, existing => MutateOnDeleted(existing, request), cancellationToken);

    public Task ApplyItemAddedAsync(RecordGiftItemAddedRequest request, CancellationToken cancellationToken) =>
        ApplyAsync(request.ListId, existing => MutateOnItemAdded(existing, request), cancellationToken);

    public Task ApplyItemRemovedAsync(RecordGiftItemRemovedRequest request, CancellationToken cancellationToken) =>
        ApplyAsync(request.ListId, existing => MutateOnItemRemoved(existing, request), cancellationToken);

    /// <exception cref="GiftListProjectionApplyExhaustedException">
    /// <see cref="BoundedCasRetryPolicy.MaxAttempts"/> compare-and-set attempts all lost to a
    /// concurrent writer. <see cref="BoundedCasRetry"/> owns the attempt-count/backoff/throw
    /// shape, including the throw itself — this method hands it a factory, not an action, so
    /// there is no way to reach this method's own exhaustion path without an exception actually
    /// being thrown.
    /// </exception>
    private Task ApplyAsync(
        Guid listId,
        Func<GiftListProjectionDocument?, (GiftListProjectionDocument Updated, bool Changed)> mutate,
        CancellationToken cancellationToken) =>
        BoundedCasRetry.RunAsync(
            _retryPolicy,
            _ => TryApplyOnceAsync(listId, mutate, cancellationToken),
            exhaustedException: () => new GiftListProjectionApplyExhaustedException(listId, _retryPolicy.MaxAttempts),
            cancellationToken);

    /// <summary>
    /// One compare-and-set attempt: read the current document (or none), compute what should
    /// change, and only write if something actually would. Returns <see langword="true"/> once
    /// there is nothing further to do — a successful write, or "nothing would change" — and
    /// <see langword="false"/> to mean a concurrent writer's own write landed first, so
    /// <see cref="BoundedCasRetry"/> should retry.
    /// </summary>
    private async Task<bool> TryApplyOnceAsync(
        Guid listId,
        Func<GiftListProjectionDocument?, (GiftListProjectionDocument Updated, bool Changed)> mutate,
        CancellationToken cancellationToken)
    {
        var existing = await _giftListProjections
            .Find(d => d.Id == listId)
            .FirstOrDefaultAsync(cancellationToken);

        var (updated, changed) = mutate(existing);
        if (!changed)
        {
            return true;
        }

        if (existing is null)
        {
            try
            {
                await _giftListProjections.InsertOneAsync(WithVersion(updated, 0), cancellationToken: cancellationToken);
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // Another handler instance inserted the stub/row first between our read and our
                // write — retry as a versioned replace against what is there now, same as a lost
                // version-filtered replace below.
                return false;
            }
        }

        var expectedVersion = existing.Version;
        var filter = Builders<GiftListProjectionDocument>.Filter.Where(
            d => d.Id == listId && d.Version == expectedVersion);
        var result = await _giftListProjections.ReplaceOneAsync(
            filter, WithVersion(updated, expectedVersion + 1), cancellationToken: cancellationToken);

        return result.MatchedCount != 0;
    }

    private static GiftListProjectionDocument WithVersion(GiftListProjectionDocument document, long version) => new()
    {
        Id = document.Id,
        HasCreated = document.HasCreated,
        ExpiresAt = document.ExpiresAt,
        IsDeleted = document.IsDeleted,
        DeletedAt = document.DeletedAt,
        Version = version,
        Items = document.Items,
    };

    /// <summary>
    /// A not-yet-fully-created row for an event whose list this projection has not seen a
    /// <c>GiftListCreatedV1</c> for yet. <see cref="GiftListProjectionDocument.HasCreated"/> stays
    /// <see langword="false"/> until <see cref="MutateOnCreated"/> itself runs, which is what
    /// keeps a stub invisible to <see cref="FindByIdAsync"/> in the meantime.
    /// </summary>
    private static GiftListProjectionDocument Stub(Guid listId) => new()
    {
        Id = listId,
        HasCreated = false,
        ExpiresAt = DateTime.MinValue,
        IsDeleted = false,
        DeletedAt = null,
        Version = 0,
        Items = [],
    };

    /// <summary>
    /// <see cref="GiftListProjectionDocument.ExpiresAt"/> has exactly one writer — GiftLists has
    /// no use case that changes a list's expiry once created — so there is no reordering to guard
    /// against here, only redelivery: the "changed" check below is what makes a second delivery
    /// of the same <c>GiftListCreatedV1</c> a no-op write.
    /// </summary>
    private static (GiftListProjectionDocument, bool) MutateOnCreated(
        GiftListProjectionDocument? existing, RecordGiftListCreatedRequest request)
    {
        var expiresAt = ProjectionInstants.ToStoredPrecision(request.ExpiresAt);
        var baseline = existing ?? Stub(request.ListId);

        var updated = new GiftListProjectionDocument
        {
            Id = request.ListId,
            HasCreated = true,
            ExpiresAt = expiresAt,
            IsDeleted = baseline.IsDeleted,
            DeletedAt = baseline.DeletedAt,
            Version = baseline.Version,
            Items = baseline.Items,
        };

        var changed = !baseline.HasCreated || baseline.ExpiresAt != updated.ExpiresAt;
        return (updated, changed);
    }

    private static (GiftListProjectionDocument, bool) MutateOnDeleted(
        GiftListProjectionDocument? existing, RecordGiftListDeletedRequest request)
    {
        var deletedAt = ProjectionInstants.ToStoredPrecision(request.DeletedAt);
        var baseline = existing ?? Stub(request.ListId);

        if (baseline.DeletedAt is { } existingDeletedAt && existingDeletedAt >= deletedAt)
        {
            return (baseline, false); // already deleted at this time or later
        }

        var updated = new GiftListProjectionDocument
        {
            Id = baseline.Id,
            HasCreated = baseline.HasCreated,
            ExpiresAt = baseline.ExpiresAt,
            IsDeleted = true,
            DeletedAt = deletedAt,
            Version = baseline.Version,
            Items = baseline.Items,
        };
        return (updated, true);
    }

    private static (GiftListProjectionDocument, bool) MutateOnItemAdded(
        GiftListProjectionDocument? existing, RecordGiftItemAddedRequest request)
    {
        var baseline = existing ?? Stub(request.ListId);
        var (items, changed) = UpsertOnAdded(
            baseline.Items, request.ItemId, ProjectionInstants.ToStoredPrecision(request.AddedAt));

        return changed ? (WithItems(baseline, items), true) : (baseline, false);
    }

    private static (GiftListProjectionDocument, bool) MutateOnItemRemoved(
        GiftListProjectionDocument? existing, RecordGiftItemRemovedRequest request)
    {
        var baseline = existing ?? Stub(request.ListId);
        var (items, changed) = UpsertOnRemoved(
            baseline.Items, request.ItemId, ProjectionInstants.ToStoredPrecision(request.RemovedAt));

        return changed ? (WithItems(baseline, items), true) : (baseline, false);
    }

    private static GiftListProjectionDocument WithItems(
        GiftListProjectionDocument baseline, List<GiftItemProjectionDocument> items) => new()
    {
        Id = baseline.Id,
        HasCreated = baseline.HasCreated,
        ExpiresAt = baseline.ExpiresAt,
        IsDeleted = baseline.IsDeleted,
        DeletedAt = baseline.DeletedAt,
        Version = baseline.Version,
        Items = items,
    };

    /// <summary>
    /// Item-keyed upsert, never an append (CONVENTIONS.md "Messaging") — an append would
    /// duplicate the item on redelivery. Last-write-wins on
    /// <see cref="GiftItemProjectionDocument.UpdatedAt"/>, ties going to the tombstone — mirrors
    /// <c>Gateway.Infrastructure.GiftLists.Persistence.GiftListProjectionRepository.UpsertOnAdded</c>'s
    /// own reasoning exactly (including why a tie must not resurrect a removed item).
    /// </summary>
    private static (List<GiftItemProjectionDocument> Items, bool Changed) UpsertOnAdded(
        List<GiftItemProjectionDocument> items, Guid itemId, DateTime addedAt)
    {
        var index = items.FindIndex(i => i.ItemId == itemId);
        var candidate = new GiftItemProjectionDocument { ItemId = itemId, IsRemoved = false, UpdatedAt = addedAt };

        if (index < 0)
        {
            return (new List<GiftItemProjectionDocument>(items) { candidate }, true);
        }

        var current = items[index];

        if (current.UpdatedAt > addedAt || (current.UpdatedAt == addedAt && current.IsRemoved))
        {
            return (items, false); // a chronologically later event (most sharply, a remove) already applied
        }

        if (current.UpdatedAt == addedAt && !current.IsRemoved)
        {
            return (items, false); // exact redelivery
        }

        var replaced = new List<GiftItemProjectionDocument>(items);
        replaced[index] = candidate;
        return (replaced, true);
    }

    /// <summary>
    /// Never deletes the array entry — see <see cref="GiftItemProjectionDocument.IsRemoved"/>'s
    /// own doc comment for why a tombstone must be left behind even (especially) when this item
    /// has never been seen before: that is exactly the case where the matching
    /// <c>GiftItemAddedV1</c> is still in flight and must lose when it eventually arrives.
    /// </summary>
    private static (List<GiftItemProjectionDocument> Items, bool Changed) UpsertOnRemoved(
        List<GiftItemProjectionDocument> items, Guid itemId, DateTime removedAt)
    {
        var index = items.FindIndex(i => i.ItemId == itemId);
        if (index < 0)
        {
            var tombstone = new GiftItemProjectionDocument { ItemId = itemId, IsRemoved = true, UpdatedAt = removedAt };
            return (new List<GiftItemProjectionDocument>(items) { tombstone }, true);
        }

        var current = items[index];
        if (current.UpdatedAt > removedAt)
        {
            return (items, false);
        }

        if (current.UpdatedAt == removedAt && current.IsRemoved)
        {
            return (items, false); // exact redelivery
        }

        var updated = new GiftItemProjectionDocument { ItemId = itemId, IsRemoved = true, UpdatedAt = removedAt };
        var replaced = new List<GiftItemProjectionDocument>(items);
        replaced[index] = updated;
        return (replaced, true);
    }
}
