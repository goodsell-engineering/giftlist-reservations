# giftlist-reservations

Reservation service: anonymous gift reservation, isolated by design. Owns the `reservation`
database and the `reservation` Rebus queue (both singular — CONVENTIONS.md "Persistence").

Built so far (GL-34 through GL-37): a `GiftListProjection` (`reservation.giftListProjections`),
kept up to date by four Rebus handlers consuming GiftLists' own `GiftListCreatedV1`/
`GiftItemAddedV1`/`GiftItemRemovedV1`/`GiftListDeletedV1` events, so this service can answer "is
this list/item reservable" without ever calling GiftLists directly (ARCHITECTURE.md "Consuming
other services' events: anti-corruption layer"); the `Reservation` aggregate and its `ReserveGift`
use case, reached over the same Rebus request/reply bridge Identity's `Login`/`SignUp` use
(ARCHITECTURE.md "Command → event flow"), enforcing "first reserver wins" with a unique
`(listId, itemId)` index (`reservation.reservations`); and `GiftReservedV1`, published after a
successful reservation. `ReleaseReservation`/`ReservationReleasedV1` — undoing a reservation — are
not built yet.

## `Reservations.Contracts`

| Kind | Types |
|---|---|
| Commands it accepts | `ReserveGift` |
| Reply shapes for those commands | `ReserveGiftReply` |
| Events it publishes | `GiftReservedV1` |

`ReserveGiftReply` carries the one-time `releaseSecret` a reserving browser needs to undo its own
reservation later (ARCHITECTURE.md "Data model") — returned here, in the reply, and nowhere else
(GL-37): never in `GiftReservedV1`, never in a query response, never in a log line. See
`GiftReservedV1PublishingTests`/`ReleaseSecretPrivacyTests` in `Reservations.IntegrationTests` for
the tests proving that end to end, against the real broker and the real database.

**Read ARCHITECTURE.md "Reservation privacy" before adding a single field.** Nobody ever sees who
reserved a gift, and nothing may correlate two reservations to one person. That guarantee is
enforced by the *absence* of data — no name, no email, no session id, no IP — and a field added
to a published contract cannot be removed again without a major bump. The cheapest way to keep
the guarantee is never to add one.

## Directory.Build.props is a copy, and it is checked

`net10.0`, `LangVersion latest`, nullable on, warnings as errors, implicit usings — set once in
`Directory.Build.props` at this repo's root, inherited by every project. No `.csproj` sets
`TargetFramework` itself (CONVENTIONS.md "Target framework").

Before the split there was one such file, at the monorepo root, and MSBuild's directory walk gave
every service the same values. MSBuild does not walk out of a repo, so each .NET repo now has its
own copy — and copies drift. **Do not hand-edit this one.** Edit the canonical copy in
`giftlist-buildingblocks`, then run `giftlist-devenv/scripts/sync-repo-roots.sh`, which rewrites
every copy and regenerates the `repo-root-files.sha256` manifest beside each.

Two tests fail if you edit it in place, and they check different things:

- `RepoRootFileSyncTests` — this copy is byte-identical to the canonical one.
- `TargetFrameworkTests.DirectoryBuildProps_ShouldMatchConventionsVerbatim` — the content is the
  block CONVENTIONS.md documents. Every repo can agree on a wrong file; this is what catches it.

The `Architecture/` suite under `tests/*.UnitTests/` is governed the same way: canonical copy in
`giftlist-giftlists`, propagated by `giftlist-devenv/scripts/sync-arch-tests.sh`, pinned by
`architecture-tests.sha256` and `ArchitectureTestSyncTests`.

## Where this repo sits

Seven repos under `goodsell-engineering`, cloned as siblings (ARCHITECTURE.md "Repository
layout"):

```
giftlist/
  local-feed/              <- .nupkg and .tgz files land here; not a git repo
  giftlist-devenv/         <- docker compose, make up, the sync scripts
  giftlist-buildingblocks/
  giftlist-gateway/
  giftlist-identity/
  giftlist-giftlists/
  giftlist-reservations/
  giftlist-web/
```

Design documents (`ARCHITECTURE.md`, `CONVENTIONS.md`) live in the workspace repository, not in
any of the seven: they govern all of them, a home inside one is invisible to the other six, and
seven copies is exactly the drift they warn about. Comments here cite them by document and
heading text, never by section number (CONVENTIONS.md "Citing the rules").

## Repo tooling

The local folder feed, this repo's `nuget.config` (GL-26), the compose mounts that make the feed
visible inside every container (GL-29), semantic versioning discipline and consumer pinning
(GL-27), `make pack-all`/`clone-all.sh` (GL-28) and per-repo CI (GL-30) have all landed — see the
nuget.config's own comments for the packageSourceMapping reasoning (dependency confusion against
nuget.org's unrelated `BuildingBlocks` and `Identity.Contracts` packages) and for why one mount
path, `- ../local-feed:/local-feed:ro`, now serves every service, .NET or web, with no
container-specific path or symlink to reconcile.

Restore and build normally:

```
dotnet restore <Solution>.sln
dotnet build <Solution>.sln --no-restore
dotnet test tests/*.UnitTests/*.UnitTests.csproj
```
