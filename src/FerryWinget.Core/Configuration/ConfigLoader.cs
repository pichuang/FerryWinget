namespace FerryWinget.Core.Configuration;

using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class ConfigLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public static FerryConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}");

        var yaml = File.ReadAllText(path);
        var config = Deserializer.Deserialize<FerryConfig>(yaml)
               ?? throw new InvalidOperationException("Failed to deserialize config");

        // Apply local override (e.g., config.local.yaml) for sensitive values
        var localPath = Path.Combine(
            Path.GetDirectoryName(path) ?? ".",
            Path.GetFileNameWithoutExtension(path) + ".local" + Path.GetExtension(path));

        if (File.Exists(localPath))
        {
            var localYaml = File.ReadAllText(localPath);
            var localConfig = Deserializer.Deserialize<FerryConfig>(localYaml);
            if (localConfig is not null)
                ApplyOverrides(config, localConfig);
        }

        return config;
    }

    public static FerryConfig LoadFromString(string yaml)
    {
        return Deserializer.Deserialize<FerryConfig>(yaml)
               ?? throw new InvalidOperationException("Failed to deserialize config");
    }

    public static string Serialize(FerryConfig config)
    {
        return Serializer.Serialize(config);
    }

    /// <summary>
    /// Applies non-default values from the override config onto the base config.
    /// Only overrides scalar fields that differ from their defaults.
    /// </summary>
    private static void ApplyOverrides(FerryConfig baseConfig, FerryConfig overrideConfig)
    {
        var defaults = new FerryConfig();

        // Source
        if (overrideConfig.Source.GithubRepo != defaults.Source.GithubRepo)
            baseConfig.Source.GithubRepo = overrideConfig.Source.GithubRepo;
        if (overrideConfig.Source.GithubToken != defaults.Source.GithubToken)
            baseConfig.Source.GithubToken = overrideConfig.Source.GithubToken;
        if (overrideConfig.Source.ManifestsBranch != defaults.Source.ManifestsBranch)
            baseConfig.Source.ManifestsBranch = overrideConfig.Source.ManifestsBranch;

        // Storage
        if (overrideConfig.Storage.RootPath != defaults.Storage.RootPath)
            baseConfig.Storage.RootPath = overrideConfig.Storage.RootPath;
        if (overrideConfig.Storage.PackagesDir != defaults.Storage.PackagesDir)
            baseConfig.Storage.PackagesDir = overrideConfig.Storage.PackagesDir;
        if (overrideConfig.Storage.ReportsDir != defaults.Storage.ReportsDir)
            baseConfig.Storage.ReportsDir = overrideConfig.Storage.ReportsDir;

        // Filtering
        if (overrideConfig.Filtering.Allowlist.Count > 0)
            baseConfig.Filtering.Allowlist = overrideConfig.Filtering.Allowlist;
        if (overrideConfig.Filtering.Blocklist.Packages.Count > 0)
            baseConfig.Filtering.Blocklist = overrideConfig.Filtering.Blocklist;

        // Retention
        if (overrideConfig.Retention.MaxMajorVersions != defaults.Retention.MaxMajorVersions)
            baseConfig.Retention.MaxMajorVersions = overrideConfig.Retention.MaxMajorVersions;

        // Downloader
        if (overrideConfig.Downloader.MaxConcurrency != defaults.Downloader.MaxConcurrency)
            baseConfig.Downloader.MaxConcurrency = overrideConfig.Downloader.MaxConcurrency;
        if (overrideConfig.Downloader.DownloadTimeoutSeconds != defaults.Downloader.DownloadTimeoutSeconds)
            baseConfig.Downloader.DownloadTimeoutSeconds = overrideConfig.Downloader.DownloadTimeoutSeconds;
        if (overrideConfig.Downloader.RetryCount != defaults.Downloader.RetryCount)
            baseConfig.Downloader.RetryCount = overrideConfig.Downloader.RetryCount;
        if (overrideConfig.Downloader.UserAgent != defaults.Downloader.UserAgent)
            baseConfig.Downloader.UserAgent = overrideConfig.Downloader.UserAgent;

        // Server
        if (overrideConfig.Server.Port != defaults.Server.Port)
            baseConfig.Server.Port = overrideConfig.Server.Port;
        if (overrideConfig.Server.SourceIdentifier != defaults.Server.SourceIdentifier)
            baseConfig.Server.SourceIdentifier = overrideConfig.Server.SourceIdentifier;

        // Firewall
        if (overrideConfig.Firewall.Enabled != defaults.Firewall.Enabled)
            baseConfig.Firewall.Enabled = overrideConfig.Firewall.Enabled;
        if (overrideConfig.Firewall.ResourceGroup != defaults.Firewall.ResourceGroup)
            baseConfig.Firewall.ResourceGroup = overrideConfig.Firewall.ResourceGroup;
        if (overrideConfig.Firewall.PolicyName != defaults.Firewall.PolicyName)
            baseConfig.Firewall.PolicyName = overrideConfig.Firewall.PolicyName;

        // Timezone
        if (overrideConfig.Timezone != defaults.Timezone)
            baseConfig.Timezone = overrideConfig.Timezone;
    }
}
