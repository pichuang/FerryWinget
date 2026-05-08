namespace FerryWinget.Downloader.Services;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Applies version retention policy: keep only the latest N major versions per package.
/// </summary>
public sealed class VersionRetentionService
{
    private readonly int _maxMajorVersions;

    public VersionRetentionService(int maxMajorVersions)
    {
        _maxMajorVersions = maxMajorVersions;
    }

    /// <summary>
    /// Given a list of version strings, returns only those in the latest N major versions.
    /// </summary>
    public List<string> Apply(IEnumerable<string> versions)
    {
        var parsed = versions
            .Select(v => (Original: v, Parsed: TryParseVersion(v)))
            .Where(x => x.Parsed is not null)
            .ToList();

        if (parsed.Count == 0)
            return [];

        // Group by major version, take top N major versions
        var topMajors = parsed
            .GroupBy(x => x.Parsed!.Major)
            .OrderByDescending(g => g.Key)
            .Take(_maxMajorVersions)
            .SelectMany(g => g)
            .Select(x => x.Original)
            .ToList();

        return topMajors;
    }

    /// <summary>
    /// Returns which versions should be removed (not in the latest N major versions).
    /// </summary>
    public List<string> GetVersionsToRemove(IEnumerable<string> versions)
    {
        var all = versions.ToList();
        var kept = Apply(all).ToHashSet();
        return all.Where(v => !kept.Contains(v)).ToList();
    }

    private static Version? TryParseVersion(string versionStr)
    {
        // Winget versions can be like "3.4.17", "3.4.17.0", "2024.11.0", etc.
        // Normalize by taking the first 4 dot-separated numeric parts
        var parts = versionStr.Split('.', '-', '+');
        var numericParts = new List<int>();

        foreach (var part in parts)
        {
            if (int.TryParse(part, out var n))
                numericParts.Add(n);
            else
                break; // stop at first non-numeric
        }

        return numericParts.Count switch
        {
            >= 4 => new Version(numericParts[0], numericParts[1], numericParts[2], numericParts[3]),
            3 => new Version(numericParts[0], numericParts[1], numericParts[2]),
            2 => new Version(numericParts[0], numericParts[1]),
            1 => new Version(numericParts[0], 0),
            _ => null
        };
    }
}
