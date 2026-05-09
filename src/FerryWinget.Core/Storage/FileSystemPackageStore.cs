namespace FerryWinget.Core.Storage;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Flat directory layout: {root}/{PackageId}/{Version}/ contains both manifest YAML and installer binaries.
/// </summary>
public sealed class FileSystemPackageStore : IPackageStore
{
    private readonly string _root;

    public FileSystemPackageStore(string rootPath, string packagesDir = "packages", string installersDir = "installers")
    {
        // Ignore legacy packagesDir/installersDir — use flat layout under rootPath/packages
        _root = Path.Combine(rootPath, packagesDir);
    }

    public async Task SaveManifestAsync(string packageId, string version, string yamlContent, CancellationToken ct = default)
    {
        var dir = GetVersionDir(packageId, version);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{packageId}.yaml");
        await File.WriteAllTextAsync(path, yamlContent, ct);
    }

    public async Task SaveInstallerAsync(string packageId, string version, string architecture, string fileName, byte[] data, CancellationToken ct = default)
    {
        var dir = GetVersionDir(packageId, version);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, data, ct);
    }

    public async Task<string?> LoadManifestAsync(string packageId, string version, CancellationToken ct = default)
    {
        var path = Path.Combine(GetVersionDir(packageId, version), $"{packageId}.yaml");
        return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;
    }

    public async Task<byte[]?> LoadInstallerAsync(string packageId, string version, string architecture, string fileName, CancellationToken ct = default)
    {
        var path = Path.Combine(GetVersionDir(packageId, version), fileName);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    public Task<bool> InstallerExistsAsync(string packageId, string version, string architecture, string fileName, CancellationToken ct = default)
    {
        var path = Path.Combine(GetVersionDir(packageId, version), fileName);
        return Task.FromResult(File.Exists(path));
    }

    public IEnumerable<string> ListPackageIds()
    {
        if (!Directory.Exists(_root))
            return [];

        return Directory.GetDirectories(_root)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .Order();
    }

    public IEnumerable<string> ListVersions(string packageId)
    {
        var pkgDir = Path.Combine(_root, packageId);
        if (!Directory.Exists(pkgDir))
            return [];

        return Directory.GetDirectories(pkgDir)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .Order();
    }

    public Task DeleteVersionAsync(string packageId, string version, CancellationToken ct = default)
    {
        var dir = GetVersionDir(packageId, version);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);

        // Clean up empty package directory
        var pkgDir = Path.Combine(_root, packageId);
        if (Directory.Exists(pkgDir) && !Directory.EnumerateFileSystemEntries(pkgDir).Any())
            Directory.Delete(pkgDir);

        return Task.CompletedTask;
    }

    private string GetVersionDir(string packageId, string version) =>
        Path.Combine(_root, packageId, version);
}
