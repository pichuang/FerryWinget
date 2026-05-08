namespace FerryWinget.Core.Tests;

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using FerryWinget.Core.Storage;

public class FileSystemPackageStoreTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(".", "tmp_download", "store-test");
    private readonly FileSystemPackageStore _store;

    public FileSystemPackageStoreTests()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
        Directory.CreateDirectory(_testRoot);
        _store = new FileSystemPackageStore(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
    }

    [Fact]
    public async Task SaveAndLoadManifest_RoundTrips()
    {
        var yaml = "PackageIdentifier: GitHub.Desktop\nPackageVersion: 3.4.0";
        await _store.SaveManifestAsync("GitHub.Desktop", "3.4.0", yaml);
        var loaded = await _store.LoadManifestAsync("GitHub.Desktop", "3.4.0");
        loaded.Should().Be(yaml);
    }

    [Fact]
    public async Task SaveAndLoadInstaller_RoundTrips()
    {
        var data = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // fake PE header
        await _store.SaveInstallerAsync("GitHub.Desktop", "3.4.0", "x64", "setup.exe", data);

        var exists = await _store.InstallerExistsAsync("GitHub.Desktop", "3.4.0", "x64", "setup.exe");
        exists.Should().BeTrue();

        var loaded = await _store.LoadInstallerAsync("GitHub.Desktop", "3.4.0", "x64", "setup.exe");
        loaded.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task InstallerExists_ReturnsFalse_WhenMissing()
    {
        var exists = await _store.InstallerExistsAsync("Missing.Pkg", "1.0", "x64", "setup.exe");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task LoadManifest_ReturnsNull_WhenMissing()
    {
        var result = await _store.LoadManifestAsync("Missing.Pkg", "1.0");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListPackageIds_ReturnsStoredPackages()
    {
        await _store.SaveManifestAsync("GitHub.Desktop", "3.4.0", "content");
        await _store.SaveManifestAsync("GitHub.CLI", "2.0.0", "content");

        var ids = _store.ListPackageIds().ToList();
        ids.Should().BeEquivalentTo(["GitHub.CLI", "GitHub.Desktop"]);
    }

    [Fact]
    public async Task ListVersions_ReturnsStoredVersions()
    {
        await _store.SaveManifestAsync("GitHub.Desktop", "3.4.0", "v1");
        await _store.SaveManifestAsync("GitHub.Desktop", "3.5.0", "v2");

        var versions = _store.ListVersions("GitHub.Desktop").ToList();
        versions.Should().BeEquivalentTo(["3.4.0", "3.5.0"]);
    }

    [Fact]
    public async Task DeleteVersion_RemovesManifestAndInstaller()
    {
        await _store.SaveManifestAsync("GitHub.Desktop", "3.4.0", "content");
        await _store.SaveInstallerAsync("GitHub.Desktop", "3.4.0", "x64", "setup.exe", [0x00]);

        await _store.DeleteVersionAsync("GitHub.Desktop", "3.4.0");

        var manifest = await _store.LoadManifestAsync("GitHub.Desktop", "3.4.0");
        manifest.Should().BeNull();
    }

    [Fact]
    public void ListPackageIds_ReturnsEmpty_WhenNoPackages()
    {
        _store.ListPackageIds().Should().BeEmpty();
    }
}
