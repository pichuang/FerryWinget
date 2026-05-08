namespace FerryWinget.Core.Configuration;

using System.Collections.Generic;

public sealed class FerryConfig
{
    public SourceConfig Source { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
    public FilteringConfig Filtering { get; set; } = new();
    public RetentionConfig Retention { get; set; } = new();
    public DownloaderConfig Downloader { get; set; } = new();
    public ServerConfig Server { get; set; } = new();
    public FirewallConfig Firewall { get; set; } = new();
    public string Timezone { get; set; } = "Asia/Taipei";
}

public sealed class SourceConfig
{
    public string GithubRepo { get; set; } = "microsoft/winget-pkgs";
    public string GithubToken { get; set; } = "";
    public string ManifestsBranch { get; set; } = "master";
}

public sealed class StorageConfig
{
    public string RootPath { get; set; } = "./mirror-data";
    public string PackagesDir { get; set; } = "packages";
    public string InstallersDir { get; set; } = "installers";
    public string ReportsDir { get; set; } = "reports";
}

public sealed class FilteringConfig
{
    public List<string> Allowlist { get; set; } = [];
    public BlocklistConfig Blocklist { get; set; } = new();
}

public sealed class BlocklistConfig
{
    public bool Enabled { get; set; } = true;
    public List<string> Publishers { get; set; } = [];
    public List<string> Packages { get; set; } = [];
}

public sealed class RetentionConfig
{
    public int MaxMajorVersions { get; set; } = 5;
}

public sealed class DownloaderConfig
{
    public int MaxConcurrency { get; set; } = 16;
    public int DownloadTimeoutSeconds { get; set; } = 300;
    public int RetryCount { get; set; } = 3;
    public string UserAgent { get; set; } = "FerryWinget/1.0";
    public int CacheTtlMinutes { get; set; } = 30;
}

public sealed class ServerConfig
{
    public int Port { get; set; } = 8080;
    public string SourceIdentifier { get; set; } = "FerryWinget";
    public List<string> SupportedApiVersions { get; set; } = ["1.4.0", "1.7.0", "1.9.0"];
}

public sealed class FirewallConfig
{
    public bool Enabled { get; set; }
    public string ResourceGroup { get; set; } = "";
    public string PolicyName { get; set; } = "";
    public string RuleCollectionGroupName { get; set; } = "rcg-winget-mirror";
    public string TlsRuleCollectionName { get; set; } = "rc-winget-tls";
    public string FqdnRuleCollectionName { get; set; } = "rc-winget-fqdn";
    public int TlsRuleCollectionPriority { get; set; } = 500;
    public int FqdnRuleCollectionPriority { get; set; } = 501;
    public List<string> SourceAddresses { get; set; } = ["10.0.0.0/8"];
}
