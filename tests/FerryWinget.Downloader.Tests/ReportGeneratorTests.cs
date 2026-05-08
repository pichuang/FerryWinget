namespace FerryWinget.Downloader.Tests;

using System.IO;
using FluentAssertions;
using FerryWinget.Core.Models;
using FerryWinget.Downloader.Services;

public class ReportGeneratorTests : IDisposable
{
    private readonly string _reportsDir = Path.Combine(".", "tmp_download", "reports-test");
    private readonly ReportGenerator _gen;

    public ReportGeneratorTests()
    {
        if (Directory.Exists(_reportsDir))
            Directory.Delete(_reportsDir, true);
        _gen = new ReportGenerator(_reportsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_reportsDir))
            Directory.Delete(_reportsDir, true);
    }

    [Fact]
    public void GenerateFullPackageList_CreatesFile()
    {
        var filterResult = new FilterResult
        {
            PlannedPackages = ["GitHub.Desktop", "GitHub.CLI"],
            BlockedPackages =
            [
                new() { PackageIdentifier = "Google.Chrome", MatchedPattern = "Google.*" }
            ],
            SkippedPackages = ["Microsoft.Edge"]
        };

        var content = _gen.GenerateFullPackageList(filterResult);

        content.Should().Contain("GitHub.Desktop");
        content.Should().Contain("Google.Chrome");
        content.Should().Contain("Google.*");
        content.Should().Contain("預計下載: **2**");
        content.Should().Contain("被封鎖: **1**");

        File.Exists(Path.Combine(_reportsDir, "full-package-list.md")).Should().BeTrue();
    }

    [Fact]
    public void GenerateDiffReport_CreatesFile()
    {
        var content = _gen.GenerateDiffReport(
            ["GitHub.NewPkg"],
            ["GitHub.Desktop"],
            ["GitHub.OldPkg"]);

        content.Should().Contain("GitHub.NewPkg");
        content.Should().Contain("GitHub.Desktop");
        content.Should().Contain("GitHub.OldPkg");
        content.Should().Contain("新增: **1**");

        File.Exists(Path.Combine(_reportsDir, "diff-report.md")).Should().BeTrue();
    }

    [Fact]
    public void GenerateFirewallFqdnReport_CreatesFile()
    {
        var analysis = new UrlAnalysisResult
        {
            Fqdns = ["github.com", "objects.githubusercontent.com"],
            TotalUrlsAnalyzed = 5,
            RedirectMap = new()
            {
                ["https://github.com/file.exe"] = ["https://github.com/file.exe", "https://objects.githubusercontent.com/file.exe"]
            }
        };

        var content = _gen.GenerateFirewallFqdnReport(analysis);

        content.Should().Contain("github.com");
        content.Should().Contain("objects.githubusercontent.com");
        content.Should().Contain("Redirect 追蹤");
        content.Should().Contain("總 FQDN 數量: **2**");

        File.Exists(Path.Combine(_reportsDir, "firewall-fqdns.md")).Should().BeTrue();
    }
}
