namespace FerryWinget.Downloader.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Analyzes installer URLs to extract unique FQDNs, including following HTTP redirects.
/// </summary>
public sealed class UrlAnalyzer : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public UrlAnalyzer(HttpClient http)
    {
        _http = http;
        _ownsClient = false;
    }

    public UrlAnalyzer()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        _http = new HttpClient(handler);
        _ownsClient = true;
    }

    /// <summary>
    /// Extracts unique FQDNs from a list of URLs, optionally following redirects.
    /// </summary>
    public async Task<UrlAnalysisResult> AnalyzeAsync(
        IEnumerable<string> urls,
        bool followRedirects = true,
        CancellationToken ct = default)
    {
        var allFqdns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var redirectMap = new Dictionary<string, List<string>>();
        var errors = new List<string>();

        var distinctUrls = urls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Extract FQDNs from original URLs
        foreach (var url in distinctUrls)
        {
            if (TryExtractFqdn(url, out var fqdn))
                allFqdns.Add(fqdn!);
        }

        // Follow redirects to discover CDN domains
        if (followRedirects)
        {
            var semaphore = new SemaphoreSlim(8);
            var tasks = distinctUrls.Select(async url =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var chain = await FollowRedirectChainAsync(url, ct);
                    if (chain.Count > 1)
                    {
                        lock (redirectMap) { redirectMap[url] = chain; }
                        foreach (var redirectUrl in chain)
                        {
                            if (TryExtractFqdn(redirectUrl, out var fqdn))
                            {
                                lock (allFqdns) { allFqdns.Add(fqdn!); }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (errors) { errors.Add($"{url}: {ex.Message}"); }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        return new UrlAnalysisResult
        {
            Fqdns = allFqdns.OrderBy(f => f).ToList(),
            RedirectMap = redirectMap,
            Errors = errors,
            TotalUrlsAnalyzed = distinctUrls.Count
        };
    }

    /// <summary>
    /// Extracts FQDNs from URLs without following redirects (fast, no HTTP calls).
    /// </summary>
    public static List<string> ExtractFqdnsFromUrls(IEnumerable<string> urls)
    {
        var fqdns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in urls)
        {
            if (TryExtractFqdn(url, out var fqdn))
                fqdns.Add(fqdn!);
        }
        return fqdns.OrderBy(f => f).ToList();
    }

    private async Task<List<string>> FollowRedirectChainAsync(string url, CancellationToken ct, int maxRedirects = 5)
    {
        var chain = new List<string> { url };

        for (int i = 0; i < maxRedirects; i++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, chain[^1]);
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is not null)
                {
                    var redirectUrl = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location.ToString()
                        : new Uri(new Uri(chain[^1]), response.Headers.Location).ToString();
                    chain.Add(redirectUrl);
                }
                else
                {
                    break;
                }
            }
            catch
            {
                break;
            }
        }

        return chain;
    }

    private static bool TryExtractFqdn(string url, out string? fqdn)
    {
        fqdn = null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            fqdn = uri.Host;
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}

public sealed class UrlAnalysisResult
{
    public List<string> Fqdns { get; set; } = [];
    public Dictionary<string, List<string>> RedirectMap { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public int TotalUrlsAnalyzed { get; set; }
}
