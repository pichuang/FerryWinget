namespace FerryWinget.Core.Models;

using System.Collections.Generic;

/// <summary>
/// Result of package filtering — categorizes packages into planned downloads and blocked.
/// </summary>
public sealed class FilterResult
{
    public List<string> PlannedPackages { get; set; } = [];
    public List<BlockedPackageEntry> BlockedPackages { get; set; } = [];
    public List<string> SkippedPackages { get; set; } = [];
}

public sealed class BlockedPackageEntry
{
    public string PackageIdentifier { get; set; } = "";
    public string MatchedPattern { get; set; } = "";
}
