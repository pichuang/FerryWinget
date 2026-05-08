namespace FerryWinget.Server.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

// --- /api/information response ---
public sealed class InformationResponse
{
    public InformationData Data { get; set; } = new();
    public string? ContinuationToken { get; set; }
}

public sealed class InformationData
{
    public string SourceIdentifier { get; set; } = "";
    public List<string> ServerSupportedVersions { get; set; } = [];
    public object? SourceAgreements { get; set; }
    public List<string> UnsupportedPackageMatchFields { get; set; } = [];
    public List<string> RequiredPackageMatchFields { get; set; } = [];
    public List<string> UnsupportedQueryParameters { get; set; } = [];
    public List<string> RequiredQueryParameters { get; set; } = [];
    public object? Authentication { get; set; }
}

// --- /api/manifestSearch request ---
public sealed class ManifestSearchRequest
{
    public int? MaximumResults { get; set; }
    public bool FetchAllManifests { get; set; }
    public SearchQuery? Query { get; set; }
    public List<SearchRequestPackageMatchFilter>? Inclusions { get; set; }
    public List<SearchRequestPackageMatchFilter>? Filters { get; set; }
}

public sealed class SearchQuery
{
    public string KeyWord { get; set; } = "";
    public string MatchType { get; set; } = "Substring";
}

public sealed class SearchRequestPackageMatchFilter
{
    public string PackageMatchField { get; set; } = "";
    public SearchRequestMatch RequestMatch { get; set; } = new();
}

public sealed class SearchRequestMatch
{
    public string KeyWord { get; set; } = "";
    public string MatchType { get; set; } = "Exact";
}

// --- /api/manifestSearch response ---
public sealed class ManifestSearchResponse
{
    public List<ManifestSearchResult> Data { get; set; } = [];
    public string? ContinuationToken { get; set; }
    public List<string> UnsupportedPackageMatchFields { get; set; } = [];
    public List<string> RequiredPackageMatchFields { get; set; } = [];
}

public sealed class ManifestSearchResult
{
    public string PackageIdentifier { get; set; } = "";
    public string PackageName { get; set; } = "";
    public string Publisher { get; set; } = "";
    public List<SearchVersion> Versions { get; set; } = [];
}

public sealed class SearchVersion
{
    public string PackageVersion { get; set; } = "";
    public string? Channel { get; set; }
    public List<string> PackageFamilyNames { get; set; } = [];
    public List<string> ProductCodes { get; set; } = [];
}

// --- /api/packageManifests response ---
public sealed class PackageManifestResponse
{
    public PackageManifestData? Data { get; set; }
    public string? ContinuationToken { get; set; }
    public List<string> UnsupportedQueryParameters { get; set; } = [];
    public List<string> RequiredQueryParameters { get; set; } = [];
}

public sealed class PackageManifestData
{
    public string PackageIdentifier { get; set; } = "";
    public List<PackageManifestVersion> Versions { get; set; } = [];
}

public sealed class PackageManifestVersion
{
    public string PackageVersion { get; set; } = "";
    public string? Channel { get; set; }
    public DefaultLocaleData? DefaultLocale { get; set; }
    public List<object> Locales { get; set; } = [];
    public List<InstallerData> Installers { get; set; } = [];
}

public sealed class DefaultLocaleData
{
    public string PackageLocale { get; set; } = "en-US";
    public string Publisher { get; set; } = "";
    public string PackageName { get; set; } = "";
    public string License { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string? Description { get; set; }
    public string? Moniker { get; set; }
    public List<string> Tags { get; set; } = [];
}

public sealed class InstallerData
{
    public string InstallerSha256 { get; set; } = "";
    public string InstallerUrl { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string? InstallerType { get; set; }
    public string? Scope { get; set; }
    public string? ProductCode { get; set; }
}
