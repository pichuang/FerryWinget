namespace FerryWinget.Downloader.Tests;

using FluentAssertions;
using FerryWinget.Downloader.Services;

public class VersionRetentionServiceTests
{
    [Fact]
    public void Apply_KeepsLatestNMajorVersions()
    {
        var service = new VersionRetentionService(maxMajorVersions: 2);
        var versions = new[] { "1.0.0", "1.1.0", "2.0.0", "2.1.0", "3.0.0", "3.1.0" };

        var result = service.Apply(versions);

        // Should keep major 3 and 2 only
        result.Should().Contain("3.0.0");
        result.Should().Contain("3.1.0");
        result.Should().Contain("2.0.0");
        result.Should().Contain("2.1.0");
        result.Should().NotContain("1.0.0");
        result.Should().NotContain("1.1.0");
    }

    [Fact]
    public void Apply_KeepsAll_WhenLessThanN()
    {
        var service = new VersionRetentionService(maxMajorVersions: 5);
        var versions = new[] { "1.0.0", "2.0.0" };

        var result = service.Apply(versions);
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Apply_HandlesComplexVersions()
    {
        var service = new VersionRetentionService(maxMajorVersions: 2);
        var versions = new[] { "2024.11.0", "2024.12.0", "2025.1.0", "2023.5.0" };

        var result = service.Apply(versions);

        result.Should().Contain("2025.1.0");
        result.Should().Contain("2024.11.0");
        result.Should().Contain("2024.12.0");
        result.Should().NotContain("2023.5.0");
    }

    [Fact]
    public void Apply_EmptyInput_ReturnsEmpty()
    {
        var service = new VersionRetentionService(5);
        service.Apply([]).Should().BeEmpty();
    }

    [Fact]
    public void GetVersionsToRemove_ReturnsCorrectOnes()
    {
        var service = new VersionRetentionService(maxMajorVersions: 1);
        var versions = new[] { "1.0.0", "2.0.0", "3.0.0" };

        var toRemove = service.GetVersionsToRemove(versions);

        toRemove.Should().BeEquivalentTo(["1.0.0", "2.0.0"]);
    }

    [Fact]
    public void Apply_Default5_KeepsFiveMajorVersions()
    {
        var service = new VersionRetentionService(maxMajorVersions: 5);
        var versions = new[] { "1.0", "2.0", "3.0", "4.0", "5.0", "6.0", "7.0" };

        var result = service.Apply(versions);

        result.Should().Contain("7.0");
        result.Should().Contain("3.0");
        result.Should().NotContain("2.0");
        result.Should().NotContain("1.0");
    }
}
