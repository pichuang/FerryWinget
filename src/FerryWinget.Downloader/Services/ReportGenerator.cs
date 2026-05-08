namespace FerryWinget.Downloader.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FerryWinget.Core.Helpers;
using FerryWinget.Core.Models;

/// <summary>
/// Generates Markdown reports for the download operation.
/// </summary>
public sealed class ReportGenerator
{
    private readonly string _reportsDir;

    public ReportGenerator(string reportsDir)
    {
        _reportsDir = reportsDir;
        Directory.CreateDirectory(_reportsDir);
    }

    /// <summary>
    /// Generates the full package list report (planned + blocked + skipped).
    /// </summary>
    public string GenerateFullPackageList(
        FilterResult filterResult,
        Dictionary<string, List<string>>? versionMap = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# FerryWinget — 完整套件清單");
        sb.AppendLine();
        sb.AppendLine($"> 產生時間: {TaipeiTimeHelper.FormatTimestamp()}");
        sb.AppendLine();

        // Planned packages
        sb.AppendLine("## 預計下載套件");
        sb.AppendLine();
        if (filterResult.PlannedPackages.Count > 0)
        {
            sb.AppendLine("| # | Package Identifier | Versions |");
            sb.AppendLine("|---|-------------------|----------|");
            for (int i = 0; i < filterResult.PlannedPackages.Count; i++)
            {
                var pkg = filterResult.PlannedPackages[i];
                var versions = versionMap?.GetValueOrDefault(pkg);
                var versionStr = versions is not null ? string.Join(", ", versions) : "—";
                sb.AppendLine($"| {i + 1} | `{pkg}` | {versionStr} |");
            }
        }
        else
        {
            sb.AppendLine("_無預計下載的套件_");
        }
        sb.AppendLine();

        // Blocked packages
        sb.AppendLine("## 被封鎖套件");
        sb.AppendLine();
        if (filterResult.BlockedPackages.Count > 0)
        {
            sb.AppendLine("| # | Package Identifier | Matched Pattern |");
            sb.AppendLine("|---|-------------------|----------------|");
            for (int i = 0; i < filterResult.BlockedPackages.Count; i++)
            {
                var pkg = filterResult.BlockedPackages[i];
                sb.AppendLine($"| {i + 1} | `{pkg.PackageIdentifier}` | `{pkg.MatchedPattern}` |");
            }
        }
        else
        {
            sb.AppendLine("_無被封鎖的套件_");
        }
        sb.AppendLine();

        // Skipped (not in allowlist)
        if (filterResult.SkippedPackages.Count > 0)
        {
            sb.AppendLine("## 略過套件 (不在允許清單中)");
            sb.AppendLine();
            sb.AppendLine($"共 {filterResult.SkippedPackages.Count} 個套件略過。");
        }
        sb.AppendLine();

        // Summary
        sb.AppendLine("## 摘要");
        sb.AppendLine();
        sb.AppendLine($"- 預計下載: **{filterResult.PlannedPackages.Count}** 個套件");
        sb.AppendLine($"- 被封鎖: **{filterResult.BlockedPackages.Count}** 個套件");
        sb.AppendLine($"- 略過: **{filterResult.SkippedPackages.Count}** 個套件");

        var content = sb.ToString();
        File.WriteAllText(Path.Combine(_reportsDir, "full-package-list.md"), content);
        return content;
    }

    /// <summary>
    /// Generates a diff report (new/updated/removed packages).
    /// </summary>
    public string GenerateDiffReport(
        List<string> newPackages,
        List<string> updatedPackages,
        List<string> removedPackages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# FerryWinget — 差異清單");
        sb.AppendLine();
        sb.AppendLine($"> 產生時間: {TaipeiTimeHelper.FormatTimestamp()}");
        sb.AppendLine();

        sb.AppendLine("## 新增套件");
        sb.AppendLine();
        if (newPackages.Count > 0)
            newPackages.ForEach(p => sb.AppendLine($"- `{p}`"));
        else
            sb.AppendLine("_無新增_");
        sb.AppendLine();

        sb.AppendLine("## 更新套件");
        sb.AppendLine();
        if (updatedPackages.Count > 0)
            updatedPackages.ForEach(p => sb.AppendLine($"- `{p}`"));
        else
            sb.AppendLine("_無更新_");
        sb.AppendLine();

        sb.AppendLine("## 移除套件");
        sb.AppendLine();
        if (removedPackages.Count > 0)
            removedPackages.ForEach(p => sb.AppendLine($"- `{p}`"));
        else
            sb.AppendLine("_無移除_");
        sb.AppendLine();

        sb.AppendLine("## 摘要");
        sb.AppendLine();
        sb.AppendLine($"- 新增: **{newPackages.Count}**");
        sb.AppendLine($"- 更新: **{updatedPackages.Count}**");
        sb.AppendLine($"- 移除: **{removedPackages.Count}**");

        var content = sb.ToString();
        File.WriteAllText(Path.Combine(_reportsDir, "diff-report.md"), content);
        return content;
    }

    /// <summary>
    /// Generates firewall FQDN report grouped by domain.
    /// </summary>
    public string GenerateFirewallFqdnReport(UrlAnalysisResult analysis)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# FerryWinget — Firewall FQDN 清單");
        sb.AppendLine();
        sb.AppendLine($"> 產生時間: {TaipeiTimeHelper.FormatTimestamp()}");
        sb.AppendLine($"> 分析 URL 數量: {analysis.TotalUrlsAnalyzed}");
        sb.AppendLine();

        // Group FQDNs by top-level domain
        sb.AppendLine("## 需要的 FQDN");
        sb.AppendLine();
        var grouped = analysis.Fqdns
            .GroupBy(f =>
            {
                var parts = f.Split('.');
                return parts.Length >= 2 ? string.Join('.', parts[^2..]) : f;
            })
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            sb.AppendLine($"### {group.Key}");
            foreach (var fqdn in group.OrderBy(f => f))
                sb.AppendLine($"- `{fqdn}`");
            sb.AppendLine();
        }

        // Redirect chains
        if (analysis.RedirectMap.Count > 0)
        {
            sb.AppendLine("## Redirect 追蹤");
            sb.AppendLine();
            foreach (var (url, chain) in analysis.RedirectMap.OrderBy(kv => kv.Key))
            {
                sb.AppendLine($"- `{chain.First()}`");
                for (int i = 1; i < chain.Count; i++)
                    sb.AppendLine($"  → `{chain[i]}`");
            }
            sb.AppendLine();
        }

        // Errors
        if (analysis.Errors.Count > 0)
        {
            sb.AppendLine("## 分析錯誤");
            sb.AppendLine();
            analysis.Errors.ForEach(e => sb.AppendLine($"- {e}"));
            sb.AppendLine();
        }

        sb.AppendLine("## 摘要");
        sb.AppendLine();
        sb.AppendLine($"- 總 FQDN 數量: **{analysis.Fqdns.Count}**");
        sb.AppendLine($"- 具有 redirect 的 URL: **{analysis.RedirectMap.Count}**");

        var content = sb.ToString();
        File.WriteAllText(Path.Combine(_reportsDir, "firewall-fqdns.md"), content);
        return content;
    }
}
