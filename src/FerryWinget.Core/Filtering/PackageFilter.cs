namespace FerryWinget.Core.Filtering;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FerryWinget.Core.Configuration;
using FerryWinget.Core.Models;

/// <summary>
/// Filters packages based on allowlist/blocklist glob patterns.
/// Blocklist has highest priority — a package matching blocklist is always excluded.
/// </summary>
public sealed class PackageFilter
{
    private readonly List<Regex> _allowPatterns;
    private readonly List<(string Pattern, Regex Regex)> _blockPatterns;
    private readonly List<(string Pattern, Regex Regex)> _blockPublisherPatterns;
    private readonly bool _blocklistEnabled;

    public PackageFilter(IEnumerable<string> allowlist, IEnumerable<string> blocklist)
        : this(allowlist, blocklist, [], blocklistEnabled: true) { }

    public PackageFilter(FilteringConfig config)
        : this(config.Allowlist, config.Blocklist.Packages, config.Blocklist.Publishers, config.Blocklist.Enabled) { }

    public PackageFilter(
        IEnumerable<string> allowlist,
        IEnumerable<string> blocklistPackages,
        IEnumerable<string> blocklistPublishers,
        bool blocklistEnabled = true)
    {
        _blocklistEnabled = blocklistEnabled;
        _allowPatterns = allowlist.Select(GlobToRegex).ToList();
        _blockPatterns = blocklistPackages.Select(p => (p, GlobToRegex(p))).ToList();
        _blockPublisherPatterns = blocklistPublishers.Select(p => (p, GlobToRegex(p))).ToList();
    }

    public FilterResult Filter(IEnumerable<string> packageIdentifiers)
    {
        var result = new FilterResult();

        foreach (var id in packageIdentifiers)
        {
            var blockMatch = FindBlockMatch(id);
            if (blockMatch is not null)
            {
                result.BlockedPackages.Add(new BlockedPackageEntry
                {
                    PackageIdentifier = id,
                    MatchedPattern = blockMatch
                });
                continue;
            }

            if (_allowPatterns.Count == 0 || _allowPatterns.Any(a => a.IsMatch(id)))
            {
                result.PlannedPackages.Add(id);
            }
            else
            {
                result.SkippedPackages.Add(id);
            }
        }

        return result;
    }

    public bool IsAllowed(string packageIdentifier)
    {
        if (IsBlocked(packageIdentifier))
            return false;

        return _allowPatterns.Count == 0 || _allowPatterns.Any(a => a.IsMatch(packageIdentifier));
    }

    public bool IsBlocked(string packageIdentifier) =>
        FindBlockMatch(packageIdentifier) is not null;

    /// <summary>
    /// Check if a publisher name matches blocklist publisher patterns.
    /// </summary>
    public bool IsPublisherBlocked(string publisher) =>
        _blocklistEnabled && _blockPublisherPatterns.Any(b => b.Regex.IsMatch(publisher));

    private string? FindBlockMatch(string packageIdentifier)
    {
        if (!_blocklistEnabled)
            return null;

        // Check package patterns
        var match = _blockPatterns.FirstOrDefault(b => b.Regex.IsMatch(packageIdentifier));
        if (match.Pattern is not null)
            return match.Pattern;

        // Check publisher patterns against the publisher prefix of the identifier
        // PackageIdentifier format: "Publisher.ProductName" — extract publisher
        var dotIndex = packageIdentifier.IndexOf('.');
        if (dotIndex > 0 && _blockPublisherPatterns.Count > 0)
        {
            var publisher = packageIdentifier[..dotIndex];
            var pubMatch = _blockPublisherPatterns.FirstOrDefault(b => b.Regex.IsMatch(publisher));
            if (pubMatch.Pattern is not null)
                return $"publisher:{pubMatch.Pattern}";
        }

        return null;
    }

    private static Regex GlobToRegex(string glob)
    {
        var pattern = "^" + Regex.Escape(glob)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
