namespace FerryWinget.Downloader.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FerryWinget.Core.Configuration;
using FerryWinget.Core.Helpers;

/// <summary>
/// Enumerates winget-pkgs manifests from the GitHub API using the Git Tree API for efficiency.
/// Supports local file cache with configurable TTL to avoid repeated API calls.
/// </summary>
public sealed class GitHubManifestClient
{
    private readonly HttpClient _http;
    private readonly SourceConfig _config;
    private readonly string? _cachePath;
    private readonly TimeSpan _cacheTtl;

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GitHubManifestClient(HttpClient http, SourceConfig config, string? cacheDir = null, int cacheTtlMinutes = 30)
    {
        _http = http;
        _config = config;
        _cacheTtl = TimeSpan.FromMinutes(cacheTtlMinutes);

        if (cacheDir is not null)
        {
            Directory.CreateDirectory(cacheDir);
            _cachePath = Path.Combine(cacheDir, "package-list-cache.json");
        }

        // GitHub API requires Accept header
        if (!_http.DefaultRequestHeaders.Accept.Any(
            h => h.MediaType?.Contains("github") == true || h.MediaType == "application/json"))
        {
            _http.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }
    }

    /// <summary>
    /// Lists all package identifiers from the winget-pkgs repo manifests directory.
    /// Handles truncated trees by fetching letter-level subtrees individually.
    /// </summary>
    public async Task<List<PackageEntry>> ListPackagesAsync(Action<string>? onProgress = null, CancellationToken ct = default)
    {
        // Try loading from cache
        var cached = TryLoadCache();
        if (cached is not null)
        {
            onProgress?.Invoke($"  使用本地快取 (有效期至 {cached.ExpiresAt:HH:mm:ss}，{cached.Packages.Count:N0} 個套件)");
            return cached.Packages;
        }

        var (owner, repo) = ParseRepo(_config.GithubRepo);
        var branch = _config.ManifestsBranch;

        // Get the tree SHA for the manifests directory
        var treeSha = await GetManifestsTreeShaAsync(owner, repo, branch, ct);
        if (treeSha is null)
            throw new InvalidOperationException("Could not find manifests directory in repo");

        // First try a full recursive tree
        var url = $"https://api.github.com/repos/{owner}/{repo}/git/trees/{treeSha}?recursive=1";
        var response = await GetTreeAsync(url, ct);

        if (!response.Truncated)
        {
            var result = ParsePackageEntries(response.Tree);
            SaveCache(result);
            return result;
        }

        // Tree is truncated — fetch each letter subtree individually
        // manifests/ contains single-letter directories: a/, b/, ..., z/, 0/, etc.
        var manifestsTree = await GetTreeAsync(
            $"https://api.github.com/repos/{owner}/{repo}/git/trees/{treeSha}", ct);

        var letterDirs = manifestsTree.Tree.Where(t => t.Type == "tree").ToList();
        var allItems = new List<GitTreeItem>();
        for (int i = 0; i < letterDirs.Count; i++)
        {
            var letterDir = letterDirs[i];
            var letterUrl = $"https://api.github.com/repos/{owner}/{repo}/git/trees/{letterDir.Sha}?recursive=1";
            var letterResponse = await GetTreeAsync(letterUrl, ct);

            // Prefix each path with the letter directory
            foreach (var item in letterResponse.Tree)
            {
                item.Path = $"{letterDir.Path}/{item.Path}";
            }
            allItems.AddRange(letterResponse.Tree);
            onProgress?.Invoke($"  列舉進度: [{i + 1}/{letterDirs.Count}] 字母 '{letterDir.Path}' — 累計 {allItems.Count:N0} 筆項目");
        }

        var packages = ParsePackageEntries(allItems);
        SaveCache(packages);
        return packages;
    }

    /// <summary>
    /// Fetches a raw file from the repo.
    /// </summary>
    public async Task<string> GetFileContentAsync(string path, CancellationToken ct = default)
    {
        var (owner, repo) = ParseRepo(_config.GithubRepo);
        var branch = _config.ManifestsBranch;
        var url = $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}";
        return await _http.GetStringAsync(url, ct);
    }

    private PackageListCache? TryLoadCache()
    {
        if (_cachePath is null || !File.Exists(_cachePath))
            return null;

        try
        {
            var json = File.ReadAllText(_cachePath);
            var cache = JsonSerializer.Deserialize<PackageListCache>(json, CacheJsonOptions);
            if (cache is null)
                return null;

            var now = TaipeiTimeHelper.Now;
            if (now < cache.ExpiresAt)
                return cache;

            // Expired
            return null;
        }
        catch
        {
            return null;
        }
    }

    private void SaveCache(List<PackageEntry> packages)
    {
        if (_cachePath is null)
            return;

        try
        {
            var cache = new PackageListCache
            {
                CreatedAt = TaipeiTimeHelper.Now,
                ExpiresAt = TaipeiTimeHelper.Now.Add(_cacheTtl),
                Packages = packages
            };
            var json = JsonSerializer.Serialize(cache, CacheJsonOptions);
            File.WriteAllText(_cachePath, json);
        }
        catch
        {
            // Cache write failure is non-fatal
        }
    }

    private async Task<GitTreeResponse> GetTreeAsync(string url, CancellationToken ct)
    {
        var httpResponse = await _http.GetAsync(url, ct);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"GitHub API error: {httpResponse.StatusCode} for {url}\n{errorBody}");
        }
        return await httpResponse.Content.ReadFromJsonAsync<GitTreeResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Failed to deserialize tree from {url}");
    }

    private async Task<string?> GetManifestsTreeShaAsync(string owner, string repo, string branch, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/git/trees/{branch}";
        var response = await GetTreeAsync(url, ct);
        return response.Tree.FirstOrDefault(t => t.Path == "manifests")?.Sha;
    }

    internal static List<PackageEntry> ParsePackageEntries(List<GitTreeItem> treeItems)
    {
        // Tree paths look like: g/GitHub/Desktop/3.4.0/GitHub.Desktop.yaml
        // We need to find version directories that contain .yaml files
        var packages = new Dictionary<string, PackageEntry>();

        foreach (var item in treeItems.Where(t => t.Type == "blob" && t.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))
        {
            var parts = item.Path.Split('/');
            // Minimum: {letter}/{Publisher}/{Product}/{Version}/{file}.yaml = 5 parts
            if (parts.Length < 5) continue;

            // Find the version directory — it's the parent of the .yaml file
            var versionDir = string.Join('/', parts[..^1]);
            var version = parts[^2];
            var fileName = parts[^1];

            // Extract PackageIdentifier from the filename (e.g., "GitHub.Desktop.yaml" → "GitHub.Desktop")
            // The base manifest file is {PackageId}.yaml (no .installer. or .locale.)
            if (!fileName.Contains(".installer.") && !fileName.Contains(".locale."))
            {
                var packageId = fileName[..^5]; // strip .yaml
                if (!packages.TryGetValue(packageId, out var entry))
                {
                    entry = new PackageEntry { PackageIdentifier = packageId };
                    packages[packageId] = entry;
                }
                entry.Versions.Add(new PackageVersionEntry
                {
                    Version = version,
                    ManifestPath = string.Join('/', parts[..^1])
                });
            }
        }

        return packages.Values.OrderBy(p => p.PackageIdentifier).ToList();
    }

    private static (string owner, string repo) ParseRepo(string fullName)
    {
        var parts = fullName.Split('/');
        return (parts[0], parts[1]);
    }

    // JSON models for GitHub API
    public sealed class GitTreeResponse
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = "";
        [JsonPropertyName("tree")]
        public List<GitTreeItem> Tree { get; set; } = [];
        [JsonPropertyName("truncated")]
        public bool Truncated { get; set; }
    }

    public sealed class GitTreeItem
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = "";
    }
}

public sealed class PackageEntry
{
    public string PackageIdentifier { get; set; } = "";
    public List<PackageVersionEntry> Versions { get; set; } = [];
}

public sealed class PackageVersionEntry
{
    public string Version { get; set; } = "";
    public string ManifestPath { get; set; } = "";
}

public sealed class PackageListCache
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public List<PackageEntry> Packages { get; set; } = [];
}
