namespace FerryWinget.Downloader.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FerryWinget.Core.Configuration;

/// <summary>
/// Deploys Azure Firewall Policy rules using az CLI.
/// Supports --dry-run mode to output commands without executing.
/// </summary>
public sealed partial class FirewallPolicyDeployer
{
    private readonly FirewallConfig _config;
    private readonly bool _dryRun;
    private readonly Func<string, string, Task<(int ExitCode, string Output, string Error)>> _executeCommand;

    // FQDN: RFC 952/1123 — letters, digits, hyphens, dots, wildcards
    [GeneratedRegex(@"^[\*]?[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$")]
    private static partial Regex SafeFqdnRegex();

    // Azure resource names: alphanumeric, hyphens, underscores
    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$")]
    private static partial Regex SafeAzureResourceNameRegex();

    // IP CIDR: basic validation for IPv4 addresses/ranges
    [GeneratedRegex(@"^[0-9\./]+$")]
    private static partial Regex SafeIpCidrRegex();

    public FirewallPolicyDeployer(FirewallConfig config, bool dryRun = false)
    {
        _config = config;
        _dryRun = dryRun;
        _executeCommand = ExecuteAzCommandAsync;
        ValidateConfig(config);
    }

    /// <summary>
    /// Constructor for testing — accepts a command executor delegate.
    /// </summary>
    internal FirewallPolicyDeployer(
        FirewallConfig config,
        bool dryRun,
        Func<string, string, Task<(int ExitCode, string Output, string Error)>> executeCommand)
    {
        _config = config;
        _dryRun = dryRun;
        _executeCommand = executeCommand;
    }

    public async Task<FirewallDeployResult> DeployAsync(List<string> fqdns, CancellationToken ct = default)
    {
        var result = new FirewallDeployResult();
        var commands = new List<string>();

        if (fqdns.Count == 0)
        {
            result.Message = "No FQDNs to deploy — skipping firewall policy update.";
            return result;
        }

        // Validate all FQDNs before proceeding
        var invalidFqdns = fqdns.Where(f => !SafeFqdnRegex().IsMatch(f)).ToList();
        if (invalidFqdns.Count > 0)
        {
            result.Message = $"Invalid FQDNs detected (rejected): {string.Join(", ", invalidFqdns.Take(5))}";
            return result;
        }

        var fqdnArgs = string.Join(" ", fqdns.Select(f => $"\"{f}\""));

        // Build source args: use IP Groups if configured, otherwise source addresses
        string sourceArgs;
        string sourceArgType;
        if (_config.SourceIpGroups.Count > 0)
        {
            sourceArgs = string.Join(" ", _config.SourceIpGroups.Select(g => $"\"{g}\""));
            sourceArgType = "--source-ip-groups";
        }
        else if (_config.SourceAddresses.Count > 0)
        {
            sourceArgs = string.Join(" ", _config.SourceAddresses.Select(a => $"\"{a}\""));
            sourceArgType = "--source-addresses";
        }
        else
        {
            result.Message = "未設定 source_addresses 或 source_ip_groups — 無法建立 firewall rule。";
            return result;
        }

        // 1. Ensure rule collection group exists
        var rcgCmd = BuildRuleCollectionGroupCreateCommand();
        commands.Add(rcgCmd);

        // 2. TLS rule collection
        var tlsCmd = BuildRuleCollectionCommand(
            _config.TlsRuleCollectionName,
            _config.TlsRuleCollectionPriority,
            "winget-tls-fqdns",
            fqdnArgs,
            sourceArgType, sourceArgs,
            enableTls: true);
        commands.Add(tlsCmd);

        // 3. FQDN-only rule collection
        var fqdnOnlyCmd = BuildRuleCollectionCommand(
            _config.FqdnRuleCollectionName,
            _config.FqdnRuleCollectionPriority,
            "winget-fqdn-only",
            fqdnArgs,
            sourceArgType, sourceArgs,
            enableTls: false);
        commands.Add(fqdnOnlyCmd);

        result.Commands = commands;

        if (_dryRun)
        {
            result.DryRun = true;
            result.Message = $"Dry-run mode: {commands.Count} commands generated. No changes applied.";
            result.ScriptContent = GenerateScript(commands, fqdns);
            return result;
        }

        // Execute commands
        foreach (var cmd in commands)
        {
            var (exitCode, output, error) = await _executeCommand("az", cmd);
            result.ExecutionResults.Add(new CommandExecutionResult
            {
                Command = $"az {cmd}",
                ExitCode = exitCode,
                Output = output,
                Error = error
            });

            if (exitCode != 0)
            {
                result.Success = false;
                result.Message = $"Command failed with exit code {exitCode}: {error}";
                return result;
            }
        }

        result.Success = true;
        result.Message = $"Successfully deployed {fqdns.Count} FQDNs to firewall policy.";
        return result;
    }

    public string GenerateScript(List<string> commands, List<string> fqdns)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("# Azure Firewall Policy deployment script for FerryWinget");
        sb.AppendLine($"# Generated at: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"# FQDNs count: {fqdns.Count}");
        sb.AppendLine($"# Resource Group: {_config.ResourceGroup}");
        sb.AppendLine($"# Policy Name: {_config.PolicyName}");
        sb.AppendLine();
        sb.AppendLine("set -euo pipefail");
        sb.AppendLine();

        sb.AppendLine("# FQDNs that need to be accessible:");
        foreach (var fqdn in fqdns)
            sb.AppendLine($"#   - {fqdn}");
        sb.AppendLine();

        foreach (var cmd in commands)
        {
            sb.AppendLine($"az {cmd}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildRuleCollectionGroupCreateCommand()
    {
        return $"network firewall policy rule-collection-group create " +
               $"--resource-group \"{_config.ResourceGroup}\" " +
               $"--policy-name \"{_config.PolicyName}\" " +
               $"--name \"{_config.RuleCollectionGroupName}\" " +
               $"--priority {_config.TlsRuleCollectionPriority}";
    }

    private string BuildRuleCollectionCommand(
        string collectionName, int priority, string ruleName,
        string fqdnArgs, string sourceArgType, string sourceArgs, bool enableTls)
    {
        var cmd = $"network firewall policy rule-collection-group collection add-filter-collection " +
                  $"--resource-group \"{_config.ResourceGroup}\" " +
                  $"--policy-name \"{_config.PolicyName}\" " +
                  $"--rule-collection-group-name \"{_config.RuleCollectionGroupName}\" " +
                  $"--name \"{collectionName}\" " +
                  $"--collection-priority {priority} " +
                  $"--action Allow " +
                  $"--rule-name \"{ruleName}\" " +
                  $"--rule-type ApplicationRule " +
                  $"--protocols Https=443 " +
                  $"{sourceArgType} {sourceArgs} " +
                  $"--target-fqdns {fqdnArgs} " +
                  $"--enable-tls-inspection {enableTls.ToString().ToLowerInvariant()}";
        return cmd;
    }

    private static async Task<(int ExitCode, string Output, string Error)> ExecuteAzCommandAsync(string program, string arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = program,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output, error);
    }

    /// <summary>
    /// Validates firewall config values to prevent command injection via config.yaml.
    /// </summary>
    private static void ValidateConfig(FirewallConfig config)
    {
        ValidateAzureResourceName(config.ResourceGroup, "resource_group");
        ValidateAzureResourceName(config.PolicyName, "policy_name");
        ValidateAzureResourceName(config.RuleCollectionGroupName, "rule_collection_group_name");
        ValidateAzureResourceName(config.TlsRuleCollectionName, "tls_rule_collection_name");
        ValidateAzureResourceName(config.FqdnRuleCollectionName, "fqdn_rule_collection_name");

        foreach (var addr in config.SourceAddresses)
        {
            if (!SafeIpCidrRegex().IsMatch(addr))
                throw new ArgumentException($"Invalid source_addresses value: '{addr}'");
        }

        foreach (var group in config.SourceIpGroups)
        {
            if (!SafeAzureResourceNameRegex().IsMatch(group))
                throw new ArgumentException($"Invalid source_ip_groups value: '{group}'");
        }
    }

    private static void ValidateAzureResourceName(string value, string paramName)
    {
        if (!string.IsNullOrEmpty(value) && !SafeAzureResourceNameRegex().IsMatch(value))
            throw new ArgumentException($"Invalid {paramName}: '{value}'. Only alphanumeric, hyphens, and underscores are allowed.");
    }
}

public sealed class FirewallDeployResult
{
    public bool Success { get; set; }
    public bool DryRun { get; set; }
    public string Message { get; set; } = "";
    public List<string> Commands { get; set; } = [];
    public string? ScriptContent { get; set; }
    public List<CommandExecutionResult> ExecutionResults { get; set; } = [];
}

public sealed class CommandExecutionResult
{
    public string Command { get; set; } = "";
    public int ExitCode { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
}
