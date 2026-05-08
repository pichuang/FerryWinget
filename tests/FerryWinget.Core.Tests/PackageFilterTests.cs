namespace FerryWinget.Core.Tests;

using FluentAssertions;
using FerryWinget.Core.Filtering;

public class PackageFilterTests
{
    [Fact]
    public void Allowlist_MatchesGlobPattern()
    {
        var filter = new PackageFilter(["GitHub.*"], []);
        filter.IsAllowed("GitHub.Desktop").Should().BeTrue();
        filter.IsAllowed("GitHub.CLI").Should().BeTrue();
        filter.IsAllowed("Microsoft.VisualStudioCode").Should().BeFalse();
    }

    [Fact]
    public void Blocklist_HasHighestPriority()
    {
        // Even if allowlist matches, blocklist wins
        var filter = new PackageFilter(["*"], ["Google.*"]);
        filter.IsAllowed("Google.Chrome").Should().BeFalse();
        filter.IsBlocked("Google.Chrome").Should().BeTrue();
        filter.IsAllowed("GitHub.Desktop").Should().BeTrue();
    }

    [Fact]
    public void Blocklist_OverridesAllowlist()
    {
        // Package matches both allowlist and blocklist — blocklist wins
        var filter = new PackageFilter(["Google.*"], ["Google.*"]);
        filter.IsAllowed("Google.Chrome").Should().BeFalse();
        filter.IsBlocked("Google.Chrome").Should().BeTrue();
    }

    [Fact]
    public void EmptyAllowlist_AllowsEverything()
    {
        var filter = new PackageFilter([], []);
        filter.IsAllowed("Anything.Package").Should().BeTrue();
    }

    [Fact]
    public void Filter_CategorisesCorrectly()
    {
        var filter = new PackageFilter(["GitHub.*"], ["Google.*"]);
        var packages = new[]
        {
            "GitHub.Desktop",
            "GitHub.CLI",
            "Google.Chrome",
            "Google.Drive",
            "Microsoft.VisualStudioCode"
        };

        var result = filter.Filter(packages);

        result.PlannedPackages.Should().BeEquivalentTo(["GitHub.Desktop", "GitHub.CLI"]);
        result.BlockedPackages.Should().HaveCount(2);
        result.BlockedPackages.Should().AllSatisfy(b => b.MatchedPattern.Should().Be("Google.*"));
        result.SkippedPackages.Should().BeEquivalentTo(["Microsoft.VisualStudioCode"]);
    }

    [Fact]
    public void GlobMatching_IsCaseInsensitive()
    {
        var filter = new PackageFilter(["github.*"], []);
        filter.IsAllowed("GitHub.Desktop").Should().BeTrue();
    }

    [Fact]
    public void QuestionMarkGlob_MatchesSingleChar()
    {
        var filter = new PackageFilter(["GitHub.CL?"], []);
        filter.IsAllowed("GitHub.CLI").Should().BeTrue();
        filter.IsAllowed("GitHub.CLIP").Should().BeFalse();
    }

    [Fact]
    public void BlocklistDisabled_AllowsEverything()
    {
        var filter = new PackageFilter(["GitHub.*"], ["GitHub.*"], [], blocklistEnabled: false);
        filter.IsAllowed("GitHub.Desktop").Should().BeTrue();
        filter.IsBlocked("GitHub.Desktop").Should().BeFalse();
    }

    [Fact]
    public void PublisherBlock_BlocksByPublisherPrefix()
    {
        var filter = new PackageFilter(["*"], [], ["Google"], blocklistEnabled: true);
        filter.IsBlocked("Google.Chrome").Should().BeTrue();
        filter.IsBlocked("Google.Drive").Should().BeTrue();
        filter.IsAllowed("GitHub.Desktop").Should().BeTrue();
    }

    [Fact]
    public void FilteringConfig_Constructor_Works()
    {
        var config = new FerryWinget.Core.Configuration.FilteringConfig
        {
            Allowlist = ["GitHub.*"],
            Blocklist = new()
            {
                Enabled = true,
                Publishers = [],
                Packages = ["Google.*", "Microsoft.VisualStudio.*.Community"]
            }
        };
        var filter = new PackageFilter(config);

        filter.IsAllowed("GitHub.Desktop").Should().BeTrue();
        filter.IsBlocked("Google.Chrome").Should().BeTrue();
        filter.IsBlocked("Microsoft.VisualStudio.2022.Community").Should().BeTrue();
        filter.IsAllowed("Microsoft.VisualStudio.2022.Enterprise").Should().BeFalse(); // not in allowlist
    }
}
