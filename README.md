# giftlist-reservations

Reservation service: anonymous gift reservation, isolated by design. Owns the `reservation`
database and the `reservation` Rebus queue (both singular — CONVENTIONS.md "Persistence").

**This service is a Phase 0 skeleton.** `Reservations.Host` wires Mongo, Rebus and health checks
and does nothing else; there is no aggregate and no use case yet. Phase 4 builds it.

## `Reservations.Contracts` — present and empty, on purpose

The project exists so Phase 4 adds types rather than types *and* packaging plumbing. It is empty
because that is the accurate statement of this service's public surface today: with no use case,
there is no command anyone may send it and no event anyone may subscribe to.

It is left empty rather than pre-populated with guesses. ARCHITECTURE.md "What 'breaking' means
for a message contract" makes a renamed type or a changed field a **major** version bump, so a
guessed contract is the expensive kind of wrong; adding the first real type later is a purely
additive minor.

What Phase 4 puts here, from ARCHITECTURE.md "Event catalogue": `ReserveGift` (request/reply) and
`ReleaseReservation` as commands, `GiftReservedV1` and `ReservationReleasedV1` as events.

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

## What does not work yet, and whose job it is

Restore needs the local folder feed, and the pieces that wire it up are the next three issues:

| Missing | Issue |
|---|---|
| `../local-feed` and a `nuget.config` per repo pointing at it alongside nuget.org | GL-26 |
| Semantic versioning discipline and consumer pinning | GL-27 |
| `make pack-all` (dependency-ordered, refuses to overwrite a version already in the feed) and `clone-all.sh` | GL-28 |
| Compose mounting the feed into the containers | GL-29 |
| Per-repo CI (build and test only; there is nowhere to publish to) | GL-30 |

Until GL-26 lands, restore by naming the feed on the command line:

```
dotnet restore <Solution>.sln -s https://api.nuget.org/v3/index.json -s ../local-feed
dotnet build <Solution>.sln --no-restore
dotnet test tests/*.UnitTests/*.UnitTests.csproj
```

**A warning for whoever writes GL-26's `nuget.config`.** nuget.org already serves unrelated
packages called `BuildingBlocks` (at 1.0.0) and `Identity.Contracts` (at 1.0.14). A consumer
configured with both nuget.org and the folder feed can satisfy an exact version from either
source, so listing the feed as a second `<add key>` is not enough — use `packageSourceMapping`
to bind these ids to the local feed. The 0.1.0 starting version was picked because it exists on
neither id upstream, which narrows the window but does not close it.
