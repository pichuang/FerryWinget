namespace FerryWinget.Server.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FerryWinget.Core.Models;
using FerryWinget.Core.Storage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Builds and maintains an in-memory index of all locally stored packages.
/// </summary>
public sealed class PackageIndexService
{
    private readonly IPackageStore _store;
    private readonly ConcurrentDictionary<string, IndexedPackage> _index = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public PackageIndexService(IPackageStore store)
    {
        _store = store;
    }

    public IReadOnlyDictionary<string, IndexedPackage> Index => _index;

    public async Task RebuildIndexAsync()
    {
        _index.Clear();

        foreach (var packageId in _store.ListPackageIds())
        {
            var versions = _store.ListVersions(packageId).ToList();
            var indexedVersions = new List<IndexedVersion>();

            foreach (var version in versions)
            {
                var yaml = await _store.LoadManifestAsync(packageId, version);
                if (yaml is null) continue;

                try
                {
                    var manifest = YamlDeserializer.Deserialize<InstallerYamlModel>(yaml);
                    indexedVersions.Add(new IndexedVersion
                    {
                        PackageVersion = version,
                        Installers = manifest.Installers?.Select(i => new IndexedInstaller
                        {
                            Architecture = i.Architecture ?? "x64",
                            InstallerUrl = i.InstallerUrl ?? "",
                            InstallerSha256 = i.InstallerSha256 ?? "",
                            InstallerType = i.InstallerType,
                            Scope = i.Scope,
                            ProductCode = i.ProductCode
                        }).ToList() ?? []
                    });
                }
                catch
                {
                    // Skip malformed manifests
                }
            }

            if (indexedVersions.Count > 0)
            {
                _index[packageId] = new IndexedPackage
                {
                    PackageIdentifier = packageId,
                    PackageName = ExtractPackageName(packageId),
                    Publisher = ExtractPublisher(packageId),
                    Versions = indexedVersions
                };
            }
        }
    }

    public IndexedPackage? GetPackage(string packageId) =>
        _index.TryGetValue(packageId, out var pkg) ? pkg : null;

    private static string ExtractPublisher(string packageId)
    {
        var dotIndex = packageId.IndexOf('.');
        return dotIndex > 0 ? packageId[..dotIndex] : packageId;
    }

    private static string ExtractPackageName(string packageId)
    {
        var dotIndex = packageId.IndexOf('.');
        return dotIndex > 0 ? packageId[(dotIndex + 1)..] : packageId;
    }
}

public sealed class IndexedPackage
{
    public string PackageIdentifier { get; set; } = "";
    public string PackageName { get; set; } = "";
    public string Publisher { get; set; } = "";
    public List<IndexedVersion> Versions { get; set; } = [];
}

public sealed class IndexedVersion
{
    public string PackageVersion { get; set; } = "";
    public List<IndexedInstaller> Installers { get; set; } = [];
}

public sealed class IndexedInstaller
{
    public string Architecture { get; set; } = "";
    public string InstallerUrl { get; set; } = "";
    public string InstallerSha256 { get; set; } = "";
    public string? InstallerType { get; set; }
    public string? Scope { get; set; }
    public string? ProductCode { get; set; }
}

// Internal model for YAML deserialization
internal sealed class InstallerYamlModel
{
    public string? PackageIdentifier { get; set; }
    public string? PackageVersion { get; set; }
    public List<InstallerYamlItem>? Installers { get; set; }
}

internal sealed class InstallerYamlItem
{
    public string? Architecture { get; set; }
    public string? InstallerUrl { get; set; }
    public string? InstallerSha256 { get; set; }
    public string? InstallerType { get; set; }
    public string? Scope { get; set; }
    public string? ProductCode { get; set; }
}
