namespace FerryWinget.Core.Storage;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class FileSystemPackageStore : IPackageStore
{
    private readonly string _packagesRoot;
    private readonly string _installersRoot;

    public FileSystemPackageStore(string rootPath, string packagesDir = "packages", string installersDir = "installers")
    {
        _packagesRoot = Path.Combine(rootPath, packagesDir);
        _installersRoot = Path.Combine(rootPath, installersDir);
    }

    public async Task SaveManifestAsync(string packageId, string version, string yamlContent, CancellationToken ct = default)
    {
        var dir = GetManifestDir(packageId, version);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{packageId}.yaml");
        await File.WriteAllTextAsync(path, yamlContent, ct);
    }

    public async Task SaveInstallerAsync(string packageId, string version, string architecture, string fileName, byte[] data, CancellationToken ct = default)
    {
        var dir = GetInstallerDir(packageId, version, architecture);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, data, ct);
    }

    public async Task<string?> LoadManifestAsync(string packageId, string version, CancellationToken ct = default)
    {
        var path = Path.Combine(GetManifestDir(packageId, version), $"{packageId}.yaml");
        return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;
    }

    public async Task<byte[]?> LoadInstallerAsync(string packageId, string version, string architecture, string fileName, CancellationToken ct = default)
    {
        var path = Path.Combine(GetInstallerDir(packageId, version, architecture), fileName);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    public Task<bool> InstallerExistsAsync(string packageId, string version, string architecture, string fileName, CancellationToken ct = default)
    {
        var path = Path.Combine(GetInstallerDir(packageId, version, architecture), fileName);
        return Task.FromResult(File.Exists(path));
    }

    public IEnumerable<string> ListPackageIds()
    {
        if (!Directory.Exists(_packagesRoot))
            return [];

        return Directory.GetDirectories(_packagesRoot)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .Order();
    }

    public IEnumerable<string> ListVersions(string packageId)
    {
        var pkgDir = Path.Combine(_packagesRoot, packageId);
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
        var manifestDir = GetManifestDir(packageId, version);
        if (Directory.Exists(manifestDir))
            Directory.Delete(manifestDir, true);

        var installerDir = GetInstallerDir(packageId, version);
        if (Directory.Exists(installerDir))
            Directory.Delete(installerDir, true);

        return Task.CompletedTask;
    }

    private string GetManifestDir(string packageId, string version) =>
        Path.Combine(_packagesRoot, packageId, version);

    private string GetInstallerDir(string packageId, string version, string? architecture = null)
    {
        var dir = Path.Combine(_installersRoot, packageId, version);
        return architecture is not null ? Path.Combine(dir, architecture) : dir;
    }
}
