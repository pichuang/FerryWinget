namespace FerryWinget.Core.Models;

using System.Collections.Generic;

/// <summary>
/// Represents a winget package with all its versions, matching the winget YAML manifest structure.
/// </summary>
public sealed class PackageManifest
{
    public string PackageIdentifier { get; set; } = "";
    public List<VersionManifest> Versions { get; set; } = [];
}

public sealed class VersionManifest
{
    public string PackageVersion { get; set; } = "";
    public string? Channel { get; set; }
    public DefaultLocaleManifest? DefaultLocale { get; set; }
    public List<LocaleManifest> Locales { get; set; } = [];
    public List<InstallerEntry> Installers { get; set; } = [];
}

public sealed class InstallerEntry
{
    public string? InstallerIdentifier { get; set; }
    public string InstallerSha256 { get; set; } = "";
    public string InstallerUrl { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string? InstallerLocale { get; set; }
    public List<string> Platform { get; set; } = [];
    public string? MinimumOsVersion { get; set; }
    public string? InstallerType { get; set; }
    public string? Scope { get; set; }
    public List<string> InstallModes { get; set; } = [];
    public string? UpgradeBehavior { get; set; }
    public List<string> Commands { get; set; } = [];
    public string? ProductCode { get; set; }
    public string? PackageFamilyName { get; set; }
    public string? ReleaseDate { get; set; }
}

public sealed class DefaultLocaleManifest
{
    public string PackageLocale { get; set; } = "en-US";
    public string Publisher { get; set; } = "";
    public string? PublisherUrl { get; set; }
    public string? PublisherSupportUrl { get; set; }
    public string? Author { get; set; }
    public string PackageName { get; set; } = "";
    public string? PackageUrl { get; set; }
    public string License { get; set; } = "";
    public string? LicenseUrl { get; set; }
    public string? Copyright { get; set; }
    public string ShortDescription { get; set; } = "";
    public string? Description { get; set; }
    public string? Moniker { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? ReleaseNotesUrl { get; set; }
}

public sealed class LocaleManifest
{
    public string PackageLocale { get; set; } = "";
    public string? Publisher { get; set; }
    public string? PackageName { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
}
