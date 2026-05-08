namespace FerryWinget.Downloader.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using FerryWinget.Core.Configuration;

/// <summary>
/// Applies hierarchical version retention with safety mechanisms:
/// 
/// Safety:
///   - Grace period: versions published within N days are always kept
///   - Pinned tags: versions matching pinned tags (e.g., "prod", "latest") are always kept
///
/// Hierarchy:
///   - Major: keep latest N
///   - Minor: latest major keeps M minors; older majors keep 1 minor each
///   - Patch: each (major, minor) keeps latest P patches
/// </summary>
public sealed class VersionRetentionService
{
    private readonly int _maxMajor;
    private readonly int _latestMajorMinorCount;
    private readonly int _olderMajorMinorCount;
    private readonly int _patchCount;
    private readonly int _gracePeriodDays;
    private readonly HashSet<string> _pinnedTags;

    public VersionRetentionService(RetentionConfig config)
        : this(config.MaxMajorVersions, config.LatestMajorMinorCount, config.OlderMajorMinorCount,
               config.PatchCount, config.GracePeriodDays, config.PinnedTags) { }

    public VersionRetentionService(
        int maxMajor = 3,
        int latestMajorMinorCount = 3,
        int olderMajorMinorCount = 1,
        int patchCount = 2,
        int gracePeriodDays = 30,
        IEnumerable<string>? pinnedTags = null)
    {
        _maxMajor = Math.Max(maxMajor, 1);
        _latestMajorMinorCount = Math.Max(latestMajorMinorCount, 1);
        _olderMajorMinorCount = Math.Max(olderMajorMinorCount, 1);
        _patchCount = Math.Max(patchCount, 1);
        _gracePeriodDays = gracePeriodDays;
        _pinnedTags = new HashSet<string>(pinnedTags ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the versions to keep based on the retention policy.
    /// </summary>
    /// <param name="versions">Version strings to evaluate.</param>
    /// <param name="versionDates">Optional: version → release date for grace period checks.</param>
    /// <param name="versionTags">Optional: version → tags for pinned tag checks.</param>
    public List<string> Apply(
        IEnumerable<string> versions,
        Dictionary<string, DateTimeOffset>? versionDates = null,
        Dictionary<string, List<string>>? versionTags = null)
    {
        var parsed = versions
            .Select(v => (Original: v, Parsed: TryParseVersion(v)))
            .Where(x => x.Parsed is not null)
            .ToList();

        if (parsed.Count == 0)
            return [];

        var kept = new HashSet<string>();
        var now = DateTimeOffset.UtcNow;

        // Safety: grace period — keep versions published within N days
        if (versionDates is not null && _gracePeriodDays > 0)
        {
            foreach (var (original, _) in parsed)
            {
                if (versionDates.TryGetValue(original, out var date) &&
                    (now - date).TotalDays <= _gracePeriodDays)
                {
                    kept.Add(original);
                }
            }
        }

        // Safety: pinned tags — keep versions with matching tags
        if (versionTags is not null && _pinnedTags.Count > 0)
        {
            foreach (var (original, _) in parsed)
            {
                if (versionTags.TryGetValue(original, out var tags) &&
                    tags.Any(t => _pinnedTags.Contains(t)))
                {
                    kept.Add(original);
                }
            }
        }

        // Hierarchy: Major → Minor → Patch
        var majorGroups = parsed
            .GroupBy(x => x.Parsed!.Major)
            .OrderByDescending(g => g.Key)
            .ToList();

        var keptMajors = majorGroups.Take(_maxMajor).ToList();

        for (int mi = 0; mi < keptMajors.Count; mi++)
        {
            var majorGroup = keptMajors[mi];
            var isLatestMajor = (mi == 0);
            var minorLimit = isLatestMajor ? _latestMajorMinorCount : _olderMajorMinorCount;

            var minorGroups = majorGroup
                .GroupBy(x => x.Parsed!.Minor)
                .OrderByDescending(g => g.Key)
                .Take(minorLimit)
                .ToList();

            foreach (var minorGroup in minorGroups)
            {
                // Keep latest N patches
                var topPatches = minorGroup
                    .OrderByDescending(x => x.Parsed!)
                    .Take(_patchCount)
                    .ToList();

                foreach (var p in topPatches)
                    kept.Add(p.Original);
            }
        }

        // Preserve original order
        return parsed
            .Where(x => kept.Contains(x.Original))
            .Select(x => x.Original)
            .ToList();
    }

    /// <summary>
    /// Returns which versions should be removed (not retained by policy).
    /// </summary>
    public List<string> GetVersionsToRemove(
        IEnumerable<string> versions,
        Dictionary<string, DateTimeOffset>? versionDates = null,
        Dictionary<string, List<string>>? versionTags = null)
    {
        var all = versions.ToList();
        var keepSet = Apply(all, versionDates, versionTags).ToHashSet();
        return all.Where(v => !keepSet.Contains(v)).ToList();
    }

    private static Version? TryParseVersion(string versionStr)
    {
        var parts = versionStr.Split('.', '-', '+');
        var numericParts = new List<int>();

        foreach (var part in parts)
        {
            if (int.TryParse(part, out var n))
                numericParts.Add(n);
            else
                break;
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
