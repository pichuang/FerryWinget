namespace FerryWinget.Downloader.Tests;

using FluentAssertions;
using FerryWinget.Downloader.Services;

public class VersionRetentionServiceTests
{
    [Fact]
    public void Apply_GitHubCli_FullScenario()
    {
        // maxMajor=3, latestMajorMinor=3, olderMajorMinor=1, patch=2
        var service = new VersionRetentionService(
            maxMajor: 3, latestMajorMinorCount: 3, olderMajorMinorCount: 1, patchCount: 2);

        var versions = new[]
        {
            "1.2.1", "1.3.0", "1.3.1", "1.4.0", "1.5.0",
            "1.6.0", "1.6.1", "1.6.2", "1.7.0", "1.8.0", "1.8.1",
            "1.9.0", "1.9.1", "1.9.2", "1.10.0", "1.10.1", "1.10.2", "1.10.3",
            "1.11.0", "1.12.0", "1.12.1", "1.13.0", "1.13.1", "1.14.0",
            "2.0.0"
        };

        var result = service.Apply(versions);

        // Major 2 (latest): top 3 minors → only minor 0 exists
        result.Should().Contain("2.0.0");

        // Major 1 (older): only 1 minor → minor 14 (latest)
        result.Should().Contain("1.14.0");

        // Older minors of major 1 should be dropped
        result.Should().NotContain("1.2.1");
        result.Should().NotContain("1.10.3");
        result.Should().NotContain("1.13.1");
    }

    [Fact]
    public void Apply_LatestMajor_KeepsMultipleMinors()
    {
        var service = new VersionRetentionService(
            maxMajor: 1, latestMajorMinorCount: 3, olderMajorMinorCount: 1, patchCount: 2);

        var versions = new[]
        {
            "3.0.0", "3.0.1", "3.1.0", "3.1.1", "3.1.2",
            "3.2.0", "3.2.1", "3.3.0"
        };

        var result = service.Apply(versions);

        // Latest major 3, top 3 minors: 3, 2, 1
        result.Should().Contain("3.3.0");       // minor 3, latest patch
        result.Should().Contain("3.2.1");       // minor 2, latest patch
        result.Should().Contain("3.2.0");       // minor 2, 2nd patch
        result.Should().Contain("3.1.2");       // minor 1, latest patch
        result.Should().Contain("3.1.1");       // minor 1, 2nd patch

        // minor 0 dropped (4th minor)
        result.Should().NotContain("3.0.0");
        result.Should().NotContain("3.0.1");

        // Old patches dropped
        result.Should().NotContain("3.1.0");    // 3rd patch of minor 1
    }

    [Fact]
    public void Apply_OlderMajor_KeepsOnlyOneMinor()
    {
        var service = new VersionRetentionService(
            maxMajor: 2, latestMajorMinorCount: 3, olderMajorMinorCount: 1, patchCount: 2);

        var versions = new[]
        {
            "1.0.0", "1.1.0", "1.2.0", "1.2.1",
            "2.0.0", "2.1.0"
        };

        var result = service.Apply(versions);

        // Major 2 (latest): top 3 minors → 1, 0
        result.Should().Contain("2.1.0");
        result.Should().Contain("2.0.0");

        // Major 1 (older): only 1 minor → minor 2 (latest), top 2 patches
        result.Should().Contain("1.2.1");
        result.Should().Contain("1.2.0");

        // Older minors dropped
        result.Should().NotContain("1.0.0");
        result.Should().NotContain("1.1.0");
    }

    [Fact]
    public void Apply_GracePeriod_KeepsRecentVersions()
    {
        var service = new VersionRetentionService(
            maxMajor: 1, latestMajorMinorCount: 1, patchCount: 1, gracePeriodDays: 30);

        var versions = new[] { "1.0.0", "2.0.0", "3.0.0" };
        var now = DateTimeOffset.UtcNow;
        var dates = new Dictionary<string, DateTimeOffset>
        {
            ["1.0.0"] = now.AddDays(-90),  // old → subject to pruning
            ["2.0.0"] = now.AddDays(-10),  // within grace → always kept
            ["3.0.0"] = now.AddDays(-5)    // within grace → always kept
        };

        var result = service.Apply(versions, versionDates: dates);

        result.Should().Contain("3.0.0");  // latest major + grace
        result.Should().Contain("2.0.0");  // grace period saves it
        result.Should().NotContain("1.0.0"); // old + pruned
    }

    [Fact]
    public void Apply_PinnedTags_KeepsTaggedVersions()
    {
        var service = new VersionRetentionService(
            maxMajor: 1, latestMajorMinorCount: 1, patchCount: 1,
            pinnedTags: ["prod", "latest"]);

        var versions = new[] { "1.0.0", "2.0.0", "3.0.0" };
        var tags = new Dictionary<string, List<string>>
        {
            ["1.0.0"] = ["prod"],     // pinned
            ["2.0.0"] = [],           // not pinned
            ["3.0.0"] = ["latest"]    // pinned
        };

        var result = service.Apply(versions, versionTags: tags);

        result.Should().Contain("1.0.0");  // pinned "prod"
        result.Should().Contain("3.0.0");  // pinned "latest" + latest major
        result.Should().NotContain("2.0.0"); // not pinned, not latest
    }

    [Fact]
    public void Apply_EmptyInput_ReturnsEmpty()
    {
        var service = new VersionRetentionService();
        service.Apply([]).Should().BeEmpty();
    }

    [Fact]
    public void Apply_SingleVersion_AlwaysKept()
    {
        var service = new VersionRetentionService(maxMajor: 1, latestMajorMinorCount: 1, patchCount: 1);
        service.Apply(["5.0.0"]).Should().BeEquivalentTo(["5.0.0"]);
    }

    [Fact]
    public void GetVersionsToRemove_ReturnsCorrectOnes()
    {
        var service = new VersionRetentionService(maxMajor: 1, latestMajorMinorCount: 1, patchCount: 1);
        var toRemove = service.GetVersionsToRemove(["1.0.0", "2.0.0", "3.0.0"]);
        toRemove.Should().BeEquivalentTo(["1.0.0", "2.0.0"]);
    }

    [Fact]
    public void Apply_YearBasedVersions()
    {
        var service = new VersionRetentionService(
            maxMajor: 2, latestMajorMinorCount: 2, olderMajorMinorCount: 1, patchCount: 1);

        var versions = new[] { "2023.5.0", "2024.11.0", "2024.12.0", "2025.1.0" };

        var result = service.Apply(versions);

        result.Should().Contain("2025.1.0");   // major 2025 (latest), minor 1
        result.Should().Contain("2024.12.0");  // major 2024 (older), only 1 minor → 12
        result.Should().NotContain("2024.11.0"); // older minor pruned
        result.Should().NotContain("2023.5.0");  // old major pruned
    }
}
