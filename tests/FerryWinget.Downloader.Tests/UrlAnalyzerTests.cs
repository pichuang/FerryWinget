namespace FerryWinget.Downloader.Tests;

using FluentAssertions;
using FerryWinget.Downloader.Services;

public class UrlAnalyzerTests
{
    [Fact]
    public void ExtractFqdnsFromUrls_ExtractsUniqueFqdns()
    {
        var urls = new[]
        {
            "https://github.com/PowerShell/PowerShell/releases/download/v7.4.7/PowerShell-7.4.7-win-x64.msi",
            "https://github.com/cli/cli/releases/download/v2.40.0/gh_2.40.0_windows_amd64.msi",
            "https://objects.githubusercontent.com/some-file.zip",
            "https://github.com/another/repo/releases/download/v1.0/file.exe"
        };

        var fqdns = UrlAnalyzer.ExtractFqdnsFromUrls(urls);

        fqdns.Should().HaveCount(2);
        fqdns.Should().Contain("github.com");
        fqdns.Should().Contain("objects.githubusercontent.com");
    }

    [Fact]
    public void ExtractFqdnsFromUrls_HandlesEmptyList()
    {
        UrlAnalyzer.ExtractFqdnsFromUrls([]).Should().BeEmpty();
    }

    [Fact]
    public void ExtractFqdnsFromUrls_IgnoresInvalidUrls()
    {
        var urls = new[] { "not-a-url", "https://valid.com/file.exe" };
        var fqdns = UrlAnalyzer.ExtractFqdnsFromUrls(urls);
        fqdns.Should().BeEquivalentTo(["valid.com"]);
    }

    [Fact]
    public void ExtractFqdnsFromUrls_IsCaseInsensitive()
    {
        var urls = new[]
        {
            "https://GitHub.COM/file1.exe",
            "https://github.com/file2.exe"
        };
        var fqdns = UrlAnalyzer.ExtractFqdnsFromUrls(urls);
        fqdns.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnalyzeAsync_WithoutRedirects_ExtractsFqdns()
    {
        var analyzer = new UrlAnalyzer();
        var urls = new[]
        {
            "https://github.com/file1.exe",
            "https://example.com/file2.exe"
        };

        var result = await analyzer.AnalyzeAsync(urls, followRedirects: false);

        result.Fqdns.Should().Contain("github.com");
        result.Fqdns.Should().Contain("example.com");
        result.TotalUrlsAnalyzed.Should().Be(2);
    }
}
