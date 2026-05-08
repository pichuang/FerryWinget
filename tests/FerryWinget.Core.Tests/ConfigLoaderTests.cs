namespace FerryWinget.Core.Tests;

using System.IO;
using FluentAssertions;
using FerryWinget.Core.Configuration;

public class ConfigLoaderTests
{
    [Fact]
    public void LoadFromString_ParsesAllSections()
    {
        var yaml = """
            source:
              github_repo: "microsoft/winget-pkgs"
              github_token: "test-token"
              manifests_branch: "main"
            storage:
              root_path: "./data"
              packages_dir: "pkgs"
              installers_dir: "inst"
              reports_dir: "rpt"
            filtering:
              allowlist:
                - "GitHub.*"
              blocklist:
                enabled: true
                publishers: []
                packages:
                  - "Google.*"
            retention:
              max_major_versions: 3
            downloader:
              max_concurrency: 8
              download_timeout_seconds: 120
              retry_count: 5
              user_agent: "Test/1.0"
            server:
              port: 9090
              source_identifier: "TestMirror"
              supported_api_versions:
                - "1.4.0"
            firewall:
              enabled: true
              resource_group: "rg-test"
              policy_name: "fw-test"
              rule_collection_group_name: "rcg-test"
              tls_rule_collection_name: "rc-tls"
              fqdn_rule_collection_name: "rc-fqdn"
              tls_rule_collection_priority: 100
              fqdn_rule_collection_priority: 101
              source_addresses:
                - "192.168.0.0/16"
            timezone: "Asia/Taipei"
            """;

        var config = ConfigLoader.LoadFromString(yaml);

        config.Source.GithubRepo.Should().Be("microsoft/winget-pkgs");
        config.Source.GithubToken.Should().Be("test-token");
        config.Storage.RootPath.Should().Be("./data");
        config.Filtering.Allowlist.Should().BeEquivalentTo(["GitHub.*"]);
        config.Filtering.Blocklist.Packages.Should().BeEquivalentTo(["Google.*"]);
        config.Filtering.Blocklist.Enabled.Should().BeTrue();
        config.Retention.MaxMajorVersions.Should().Be(3);
        config.Downloader.MaxConcurrency.Should().Be(8);
        config.Server.Port.Should().Be(9090);
        config.Firewall.Enabled.Should().BeTrue();
        config.Firewall.ResourceGroup.Should().Be("rg-test");
        config.Firewall.PolicyName.Should().Be("fw-test");
        config.Firewall.SourceAddresses.Should().Contain("192.168.0.0/16");
        config.Timezone.Should().Be("Asia/Taipei");
    }

    [Fact]
    public void Load_FromFile_Works()
    {
        var tmpDir = Path.Combine(".", "tmp_download", "config-test");
        Directory.CreateDirectory(tmpDir);
        var path = Path.Combine(tmpDir, "test-config.yaml");

        try
        {
            File.WriteAllText(path, """
                source:
                  github_repo: "test/repo"
                timezone: "Asia/Taipei"
                """);

            var config = ConfigLoader.Load(path);
            config.Source.GithubRepo.Should().Be("test/repo");
            config.Timezone.Should().Be("Asia/Taipei");
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        var act = () => ConfigLoader.Load("/nonexistent/config.yaml");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Defaults_AreReasonable()
    {
        var config = ConfigLoader.LoadFromString("{}");

        config.Source.GithubRepo.Should().Be("microsoft/winget-pkgs");
        config.Retention.MaxMajorVersions.Should().Be(3);
        config.Downloader.MaxConcurrency.Should().Be(16);
        config.Server.Port.Should().Be(8080);
        config.Firewall.Enabled.Should().BeFalse();
        config.Timezone.Should().Be("Asia/Taipei");
    }
}
