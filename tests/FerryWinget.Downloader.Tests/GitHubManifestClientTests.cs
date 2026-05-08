namespace FerryWinget.Downloader.Tests;

using FluentAssertions;
using FerryWinget.Downloader.Services;
using static FerryWinget.Downloader.Services.GitHubManifestClient;

public class GitHubManifestClientTests
{
    [Fact]
    public void ParsePackageEntries_ExtractsPackagesFromTree()
    {
        var treeItems = new List<GitTreeItem>
        {
            new() { Path = "g/GitHub/Desktop/3.4.0/GitHub.Desktop.yaml", Type = "blob" },
            new() { Path = "g/GitHub/Desktop/3.4.0/GitHub.Desktop.installer.yaml", Type = "blob" },
            new() { Path = "g/GitHub/Desktop/3.4.0/GitHub.Desktop.locale.en-US.yaml", Type = "blob" },
            new() { Path = "g/GitHub/Desktop/3.5.0/GitHub.Desktop.yaml", Type = "blob" },
            new() { Path = "g/GitHub/CLI/2.40.0/GitHub.CLI.yaml", Type = "blob" },
        };

        var packages = GitHubManifestClient.ParsePackageEntries(treeItems);

        packages.Should().HaveCount(2);

        var desktop = packages.First(p => p.PackageIdentifier == "GitHub.Desktop");
        desktop.Versions.Should().HaveCount(2);
        desktop.Versions.Should().Contain(v => v.Version == "3.4.0");
        desktop.Versions.Should().Contain(v => v.Version == "3.5.0");

        var cli = packages.First(p => p.PackageIdentifier == "GitHub.CLI");
        cli.Versions.Should().HaveCount(1);
    }

    [Fact]
    public void ParsePackageEntries_IgnoresNonYamlFiles()
    {
        var treeItems = new List<GitTreeItem>
        {
            new() { Path = "g/GitHub/Desktop/3.4.0/GitHub.Desktop.yaml", Type = "blob" },
            new() { Path = "g/GitHub/Desktop/3.4.0/README.md", Type = "blob" },
            new() { Path = "g/GitHub/Desktop", Type = "tree" },
        };

        var packages = GitHubManifestClient.ParsePackageEntries(treeItems);
        packages.Should().HaveCount(1);
    }

    [Fact]
    public void ParsePackageEntries_IgnoresShortPaths()
    {
        var treeItems = new List<GitTreeItem>
        {
            new() { Path = "README.yaml", Type = "blob" },
            new() { Path = "a/b.yaml", Type = "blob" },
        };

        var packages = GitHubManifestClient.ParsePackageEntries(treeItems);
        packages.Should().BeEmpty();
    }
}
