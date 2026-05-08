namespace FerryWinget.Server.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using FerryWinget.Server.Models;

/// <summary>
/// Implements winget REST source search logic:
/// Query (broad keyword), Inclusions (OR), Filters (AND).
/// </summary>
public sealed class SearchService
{
    private readonly PackageIndexService _indexService;

    public SearchService(PackageIndexService indexService)
    {
        _indexService = indexService;
    }

    public ManifestSearchResponse Search(ManifestSearchRequest request)
    {
        var candidates = _indexService.Index.Values.AsEnumerable();

        // Apply Query (broad keyword search)
        if (request.Query is not null && !string.IsNullOrWhiteSpace(request.Query.KeyWord))
        {
            candidates = candidates.Where(p =>
                MatchesField(p.PackageIdentifier, request.Query.KeyWord, request.Query.MatchType) ||
                MatchesField(p.PackageName, request.Query.KeyWord, request.Query.MatchType) ||
                MatchesField(p.Publisher, request.Query.KeyWord, request.Query.MatchType));
        }

        // Apply Inclusions (OR — at least one must match)
        if (request.Inclusions is { Count: > 0 })
        {
            candidates = candidates.Where(p =>
                request.Inclusions.Any(inc => MatchesPackageField(p, inc)));
        }

        // Apply Filters (AND — all must match)
        if (request.Filters is { Count: > 0 })
        {
            candidates = candidates.Where(p =>
                request.Filters.All(f => MatchesPackageField(p, f)));
        }

        var maxResults = request.MaximumResults ?? 100;
        var results = candidates.Take(maxResults).Select(ToSearchResult).ToList();

        return new ManifestSearchResponse { Data = results };
    }

    private static ManifestSearchResult ToSearchResult(IndexedPackage pkg) => new()
    {
        PackageIdentifier = pkg.PackageIdentifier,
        PackageName = pkg.PackageName,
        Publisher = pkg.Publisher,
        Versions = pkg.Versions.Select(v => new SearchVersion
        {
            PackageVersion = v.PackageVersion,
            ProductCodes = v.Installers
                .Where(i => i.ProductCode is not null)
                .Select(i => i.ProductCode!)
                .Distinct()
                .ToList()
        }).ToList()
    };

    private static bool MatchesPackageField(IndexedPackage pkg, SearchRequestPackageMatchFilter filter)
    {
        var fieldValue = filter.PackageMatchField switch
        {
            "PackageIdentifier" => pkg.PackageIdentifier,
            "PackageName" => pkg.PackageName,
            "Publisher" => pkg.Publisher,
            "ProductCode" => string.Join("|", pkg.Versions
                .SelectMany(v => v.Installers)
                .Where(i => i.ProductCode is not null)
                .Select(i => i.ProductCode!)),
            _ => null
        };

        if (fieldValue is null) return false;

        // For ProductCode, check if any individual code matches
        if (filter.PackageMatchField == "ProductCode")
        {
            return pkg.Versions
                .SelectMany(v => v.Installers)
                .Where(i => i.ProductCode is not null)
                .Any(i => MatchesField(i.ProductCode!, filter.RequestMatch.KeyWord, filter.RequestMatch.MatchType));
        }

        return MatchesField(fieldValue, filter.RequestMatch.KeyWord, filter.RequestMatch.MatchType);
    }

    private static bool MatchesField(string value, string keyword, string matchType)
    {
        return matchType switch
        {
            "Exact" => string.Equals(value, keyword, StringComparison.Ordinal),
            "CaseInsensitive" => string.Equals(value, keyword, StringComparison.OrdinalIgnoreCase),
            "StartsWith" => value.StartsWith(keyword, StringComparison.OrdinalIgnoreCase),
            "Substring" => value.Contains(keyword, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
