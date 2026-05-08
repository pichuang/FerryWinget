using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FerryWinget.Core.Configuration;
using FerryWinget.Core.Filtering;
using FerryWinget.Core.Helpers;
using FerryWinget.Core.Storage;
using FerryWinget.Downloader.Services;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FerryWinget.Downloader;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var configPath = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "config.yaml";
        var dryRun = args.Contains("--dry-run");

        Console.WriteLine($"FerryWinget Downloader v1.0");
        Console.WriteLine($"時間: {TaipeiTimeHelper.FormatTimestamp()}");
        Console.WriteLine($"設定檔: {configPath}");
        if (dryRun) Console.WriteLine("模式: --dry-run (不會執行 Azure Firewall 部署)");
        Console.WriteLine();

        FerryConfig config;
        try
        {
            config = ConfigLoader.Load(configPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"無法載入設定檔: {ex.Message}");
            return 1;
        }

        // Setup HTTP client
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(config.Downloader.UserAgent);
        if (!string.IsNullOrEmpty(config.Source.GithubToken))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.Source.GithubToken);

        // Setup services
        var store = new FileSystemPackageStore(config.Storage.RootPath, config.Storage.PackagesDir, config.Storage.InstallersDir);
        var filter = new PackageFilter(config.Filtering);
        var retention = new VersionRetentionService(config.Retention);
        var downloader = new InstallerDownloader(http, store, config.Downloader);
        var reportsDir = Path.Combine(config.Storage.RootPath, config.Storage.ReportsDir);
        var reportGen = new ReportGenerator(reportsDir);

        try
        {
            // Step 1: List packages from GitHub
            Console.WriteLine("步驟 1: 列舉 winget-pkgs 套件...");
            var cacheDir = Path.Combine(config.Storage.RootPath, ".cache");
            var client = new GitHubManifestClient(http, config.Source, cacheDir, config.Downloader.CacheTtlMinutes);
            var packages = await client.ListPackagesAsync(
                onProgress: msg => Console.WriteLine(msg));
            Console.WriteLine($"  找到 {packages.Count} 個套件");

            // Step 2: Filter packages
            Console.WriteLine("步驟 2: 套用 allowlist/blocklist 篩選...");
            var filterResult = filter.Filter(packages.Select(p => p.PackageIdentifier));
            Console.WriteLine($"  預計下載: {filterResult.PlannedPackages.Count}");
            Console.WriteLine($"  被封鎖: {filterResult.BlockedPackages.Count}");
            Console.WriteLine($"  略過: {filterResult.SkippedPackages.Count}");

            // Step 3: Apply version retention
            Console.WriteLine("步驟 3: 套用版本保留策略...");
            var plannedPackages = packages
                .Where(p => filterResult.PlannedPackages.Contains(p.PackageIdentifier))
                .ToList();

            var versionMap = new Dictionary<string, List<string>>();
            foreach (var pkg in plannedPackages)
            {
                var versionStrings = pkg.Versions.Select(v => v.Version).ToList();
                var retained = retention.Apply(versionStrings);
                versionMap[pkg.PackageIdentifier] = retained;
            }

            // Step 4: Track existing packages for diff
            var existingPackageIds = store.ListPackageIds().ToHashSet();

            // Step 5: Download manifests and installers
            Console.WriteLine("步驟 4: 下載 manifest 和 installer...");
            var allInstallerUrls = new List<string>();
            var downloadRequests = new List<DownloadRequest>();
            var yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var manifestCount = 0;
            var totalManifests = plannedPackages.Sum(p => p.Versions.Count(v => versionMap[p.PackageIdentifier].Contains(v.Version)));
            foreach (var pkg in plannedPackages)
            {
                var retainedVersions = versionMap[pkg.PackageIdentifier];
                foreach (var ver in pkg.Versions.Where(v => retainedVersions.Contains(v.Version)))
                {
                    manifestCount++;
                    if (manifestCount % 100 == 0 || manifestCount == totalManifests)
                        Console.WriteLine($"  manifest 取得進度: [{manifestCount}/{totalManifests}]");

                    // Download manifest files
                    var manifestPath = $"manifests/{ver.ManifestPath}";

                    try
                    {
                        // Get installer manifest
                        var installerYamlPath = $"{manifestPath}/{pkg.PackageIdentifier}.installer.yaml";
                        var installerYaml = await client.GetFileContentAsync(installerYamlPath);
                        await store.SaveManifestAsync(pkg.PackageIdentifier, ver.Version, installerYaml);

                        // Parse installer URLs
                        var installerManifest = yamlDeserializer.Deserialize<InstallerYamlManifest>(installerYaml);
                        if (installerManifest?.Installers is not null)
                        {
                            foreach (var inst in installerManifest.Installers)
                            {
                                if (string.IsNullOrEmpty(inst.InstallerUrl)) continue;
                                allInstallerUrls.Add(inst.InstallerUrl);
                                var uri = new Uri(inst.InstallerUrl);
                                var fileName = Path.GetFileName(uri.LocalPath);
                                downloadRequests.Add(new DownloadRequest
                                {
                                    PackageId = pkg.PackageIdentifier,
                                    Version = ver.Version,
                                    Architecture = inst.Architecture ?? "x64",
                                    InstallerUrl = inst.InstallerUrl,
                                    FileName = fileName,
                                    ExpectedSha256 = inst.InstallerSha256 ?? ""
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  警告: 無法處理 {pkg.PackageIdentifier} {ver.Version}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"  下載 {downloadRequests.Count} 個 installer...");
            var results = await downloader.DownloadBatchAsync(
                downloadRequests,
                onProgress: (done, total) => Console.WriteLine($"  下載進度: [{done}/{total}]"));
            var succeeded = results.Count(r => r.Success);
            var skipped = results.Count(r => r.Skipped);
            var failed = results.Count(r => !r.Success);
            Console.WriteLine($"  成功: {succeeded}, 略過 (已存在): {skipped}, 失敗: {failed}");

            // Step 5: URL analysis
            Console.WriteLine("步驟 5: 分析 installer URL FQDN...");
            var urlAnalyzer = new UrlAnalyzer();
            var analysis = await urlAnalyzer.AnalyzeAsync(allInstallerUrls, followRedirects: true);
            Console.WriteLine($"  發現 {analysis.Fqdns.Count} 個不重複 FQDN");

            // Step 6: Generate reports
            Console.WriteLine("步驟 6: 產生報告...");
            reportGen.GenerateFullPackageList(filterResult, versionMap);
            Console.WriteLine($"  已產生 full-package-list.md");

            var newPkgs = filterResult.PlannedPackages.Where(p => !existingPackageIds.Contains(p)).ToList();
            var updatedPkgs = filterResult.PlannedPackages.Where(p => existingPackageIds.Contains(p)).ToList();
            var removedPkgs = existingPackageIds.Where(p => !filterResult.PlannedPackages.Contains(p)).ToList();
            reportGen.GenerateDiffReport(newPkgs, updatedPkgs, removedPkgs);
            Console.WriteLine($"  已產生 diff-report.md");

            reportGen.GenerateFirewallFqdnReport(analysis);
            Console.WriteLine($"  已產生 firewall-fqdns.md");

            // Step 7: Azure Firewall Policy deployment
            if (config.Firewall.Enabled && analysis.Fqdns.Count > 0)
            {
                Console.WriteLine("步驟 7: 部署 Azure Firewall Policy...");
                var fwDeployer = new FirewallPolicyDeployer(config.Firewall, dryRun);
                var fwResult = await fwDeployer.DeployAsync(analysis.Fqdns);
                Console.WriteLine($"  {fwResult.Message}");

                if (fwResult.DryRun && fwResult.ScriptContent is not null)
                {
                    var scriptPath = Path.Combine(reportsDir, "firewall-commands.sh");
                    File.WriteAllText(scriptPath, fwResult.ScriptContent);
                    Console.WriteLine($"  腳本已儲存至: {scriptPath}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"完成! 時間: {TaipeiTimeHelper.FormatTimestamp()}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"錯誤: {ex.Message}");
            return 1;
        }
    }
}

// Internal model for deserializing installer YAML
internal sealed class InstallerYamlManifest
{
    public string? PackageIdentifier { get; set; }
    public string? PackageVersion { get; set; }
    public List<InstallerYamlEntry>? Installers { get; set; }
}

internal sealed class InstallerYamlEntry
{
    public string? Architecture { get; set; }
    public string? InstallerUrl { get; set; }
    public string? InstallerSha256 { get; set; }
    public string? InstallerType { get; set; }
    public string? Scope { get; set; }
    public string? ProductCode { get; set; }
}
