using ArchitectureTests.Support;

namespace ArchitectureTests;

/// <summary>
/// Binds CONVENTIONS.md "Messaging"'s blind-insert rule to a real file — mirrors
/// <c>Gateway.UnitTests/Architecture/ProjectionWriteBindingTests</c>'s own doc comment and
/// reasoning exactly, pointed at this repo's own projection instead
/// (<c>GiftListProjectionRepository</c> here builds <c>reservation.giftListProjections</c> from
/// GiftLists' integration events, GL-34 — a second projection in the system, not the Gateway's
/// only one; GL-115 is the fold that corrected that old claim wherever a PR was already touching
/// the file it sat on).
///
/// <para><b>Why this exists.</b> <c>ProjectionWriteRuleTests</c> is two tests, and neither one
/// constrains the detector against production code. The scan skips any file the detector does
/// not recognise and reports only what it finds, so a detector that has gone blind produces an
/// empty result set and a green tick. The probe self-test pins the detector's verdicts against
/// hand-written samples — which constrains it as applied to those samples, and to nothing else.
/// A named real file is what constrains it against production code.</para>
///
/// <para>If <see cref="ProjectionRepositoryPath"/> is renamed or moved, update it here rather
/// than deleting this test — a rename is exactly the moment the binding matters most.</para>
/// </summary>
public class ProjectionWriteBindingTests
{
    private const string ProjectionRepositoryPath =
        "src/Reservations.Infrastructure/GiftLists/Persistence/GiftListProjectionRepository.cs";

    [Fact]
    public void TheBlindInsertProbe_ShouldStillSeeTheRealProjectionRepository()
    {
        // Arrange
        var path = Path.Combine(RepoDiscovery.RepoRoot, ProjectionRepositoryPath.Replace('/', Path.DirectorySeparatorChar));
        var failures = new List<string>();

        // Act
        var text = File.Exists(path) ? File.ReadAllText(path) : null;
        var recognised = text is not null && ProjectionWriteProbe.IsProjectionPersistenceFile(text);
        var sites = text is null ? [] : ProjectionWriteProbe.FindWriteSites(text);

        if (text is null)
        {
            failures.Add(
                $"'{ProjectionRepositoryPath}' does not exist. If it moved, point this test at its " +
                "new home; the CONVENTIONS.md \"Messaging\" blind-insert rule has no other binding to real code");
        }
        else
        {
            if (!recognised)
            {
                failures.Add(
                    "the detector no longer recognises it as projection persistence code, so the " +
                    "CONVENTIONS.md \"Messaging\" scan silently skips it and passes on an empty population");
            }

            if (sites.Count == 0)
            {
                failures.Add(
                    "the detector found no Mongo write site in it at all, so the CONVENTIONS.md \"Messaging\" scan has " +
                    "nothing to judge and passes vacuously — note the per-file stale guard cannot " +
                    "catch this, because this file also reads");
            }
        }

        // Assert
        Assert.True(failures.Count == 0,
            $"The CONVENTIONS.md \"Messaging\" blind-insert rule has come unbound from '{ProjectionRepositoryPath}', this " +
            "repo's own projection: " + string.Join("; ", failures) +
            $". Detector said (recognised: {recognised}, write sites: {sites.Count}). Fix the " +
            "detector in Support/ProjectionWriteProbe.cs — the rule is not enforcing anything " +
            "against production code until this passes.");
    }
}
