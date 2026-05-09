namespace FerryWinget.Core.Storage;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Flat directory layout: {root}/{PackageId}/{Version}/ contains both manifest YAML and installer binaries.
/// </summary>
public sealed partial class FileSystemPackageStore : IPackageStore
{
    private readonly string _root;

    // Only allow safe characters in path components (letters, digits, dots, hyphens, underscores, plus)
    [GeneratedRegex(@"^[a-zA-Z0-9._\-+]+$")]
    private static partial Regex SafePathComponentRegex();

    public FileSystemPackageStore(string rootPath, string packagesDir = "packages", string installersDir = "installers")
    {
        // Ignore legacy packagesDir/installersDir — use flat layout under rootPath/packages
        _root = Path.GetFullPath(Path.Combine(rootPath, packagesDir));
    }

    public async Task SaveManifestAsync(string packageId, string version, string yamlContent, CancellationToken ct = default)
    {
        ValidatePathComponent(packageId, nameof(packageId));
        ValidatePathComponent(version, nameof(version));

        var dir = GetVersionDir(packageId, version);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{packageId}.yaml");
        await File.WriteAllTextAsync(path, yamlContent, ct);
    }

    public async Task SaveInstallerAsync(string packageId, string version, string architecture, string fileName, byte[] data, CancellationToken ct = default)
    {
        ValidatePathComponent(packageId, nameof(packageId));
        ValidatePathComponent(version, nameof(version));
        ValidateFileName(fileName);

        var dir = GetVersionDir(packageId, version);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        EnsurePathWithinRoot(path);
        await File.WriteAllBytesAsync(path, data, ct);
    }

    public async Task<string?> LoadManifestAsync(string packageId, string version, CancellationToken ct = default)
    {
        ValidatePathComponent(packageId, nameof(packageId));
        ValidatePathComponent(version, nameof(version));

        var path = Path.Combine(GetVersionDir(packageId, version), $"{packageId}.yaml");
        EnsurePathWithinRoot(path);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;
    }

    public async Task<byte[]?> LoadInstallerAsync(string packageId, string version, string architecture, string fileName, CancellationToken ct = default)
    {
        ValidatePathComponent(packageId, nameof(packageId));
        ValidatePathComponent(version, nameof(version));
        ValidateFileName(fileName);

        var path = Path.Combine(GetVersionDir(packageId, version), fileName);
        EnsurePathWithinRoot(path);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    public Task<bool> InstallerExistsAsync(string packageId, string version, string architecture, string fileName, CancellationToken ct = default)
    {
        ValidatePathComponent(packageId, nameof(packageId));
        ValidatePathComponent(version, nameof(version));
        ValidateFileName(fileName);

        var path = Path.Combine(GetVersionDir(packageId, version), fileName);
        EnsurePathWithinRoot(path);
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
        ValidatePathComponent(packageId, nameof(packageId));

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
        ValidatePathComponent(packageId, nameof(packageId));
        ValidatePathComponent(version, nameof(version));

        var dir = GetVersionDir(packageId, version);
        EnsurePathWithinRoot(dir);

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

    /// <summary>
    /// Validates that a path component (packageId, version) contains only safe characters.
    /// Prevents path traversal via "..", "/", "\", or other special characters.
    /// </summary>
    private static void ValidatePathComponent(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"'{paramName}' cannot be empty.", paramName);

        if (!SafePathComponentRegex().IsMatch(value))
            throw new ArgumentException(
                $"'{paramName}' contains invalid characters: '{value}'. Only alphanumeric, dots, hyphens, underscores, and plus signs are allowed.",
                paramName);
    }

    /// <summary>
    /// Validates that a filename is safe (no path separators, no traversal).
    /// </summary>
    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName cannot be empty.", nameof(fileName));

        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
            throw new ArgumentException($"Invalid filename: '{fileName}'.", nameof(fileName));

        if (Path.GetFileName(fileName) != fileName)
            throw new ArgumentException($"Filename must not contain path separators: '{fileName}'.", nameof(fileName));
    }

    /// <summary>
    /// Ensures a resolved path is within the root directory (defense-in-depth).
    /// </summary>
    private void EnsurePathWithinRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                $"Access denied: path '{path}' resolves outside the storage root.");
    }
}
