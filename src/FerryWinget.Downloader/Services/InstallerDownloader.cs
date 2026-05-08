namespace FerryWinget.Downloader.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FerryWinget.Core.Configuration;
using FerryWinget.Core.Storage;

/// <summary>
/// Downloads installer binaries with SemaphoreSlim concurrency control and SHA256 verification.
/// </summary>
public sealed class InstallerDownloader
{
    private readonly HttpClient _http;
    private readonly IPackageStore _store;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _retryCount;
    private readonly TimeSpan _timeout;

    public InstallerDownloader(HttpClient http, IPackageStore store, DownloaderConfig config)
    {
        _http = http;
        _store = store;
        _semaphore = new SemaphoreSlim(config.MaxConcurrency);
        _retryCount = config.RetryCount;
        _timeout = TimeSpan.FromSeconds(config.DownloadTimeoutSeconds);
    }

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            // Check if already downloaded
            if (await _store.InstallerExistsAsync(request.PackageId, request.Version, request.Architecture, request.FileName, ct))
            {
                return new DownloadResult
                {
                    Request = request,
                    Success = true,
                    Skipped = true
                };
            }

            for (int attempt = 1; attempt <= _retryCount; attempt++)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(_timeout);

                    var data = await _http.GetByteArrayAsync(request.InstallerUrl, cts.Token);

                    // Verify SHA256
                    if (!string.IsNullOrEmpty(request.ExpectedSha256))
                    {
                        var actualHash = Convert.ToHexString(SHA256.HashData(data));
                        if (!string.Equals(actualHash, request.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            return new DownloadResult
                            {
                                Request = request,
                                Success = false,
                                Error = $"SHA256 mismatch: expected {request.ExpectedSha256}, got {actualHash}"
                            };
                        }
                    }

                    await _store.SaveInstallerAsync(request.PackageId, request.Version, request.Architecture, request.FileName, data, ct);

                    return new DownloadResult { Request = request, Success = true };
                }
                catch (Exception ex) when (attempt < _retryCount && ex is not OperationCanceledException)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                }
            }

            return new DownloadResult
            {
                Request = request,
                Success = false,
                Error = $"Failed after {_retryCount} attempts"
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<DownloadResult>> DownloadBatchAsync(
        IEnumerable<DownloadRequest> requests,
        Action<int, int>? onProgress = null,
        CancellationToken ct = default)
    {
        var requestList = requests.ToList();
        var total = requestList.Count;
        var completed = 0;
        var results = new DownloadResult[total];

        var tasks = requestList.Select(async (r, index) =>
        {
            var result = await DownloadAsync(r, ct);
            results[index] = result;
            var current = Interlocked.Increment(ref completed);
            if (current % 100 == 0 || current == total)
                onProgress?.Invoke(current, total);
            return result;
        });

        await Task.WhenAll(tasks);
        return results.ToList();
    }
}

public sealed class DownloadRequest
{
    public string PackageId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string InstallerUrl { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ExpectedSha256 { get; set; } = "";
}

public sealed class DownloadResult
{
    public DownloadRequest Request { get; set; } = new();
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string? Error { get; set; }
}
