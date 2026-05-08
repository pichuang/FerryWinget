namespace FerryWinget.Downloader.Tests;

using FluentAssertions;
using FerryWinget.Core.Configuration;
using FerryWinget.Downloader.Services;

public class FirewallPolicyDeployerTests
{
    private static FirewallConfig CreateTestConfig() => new()
    {
        Enabled = true,
        ResourceGroup = "rg-test",
        PolicyName = "fw-test",
        RuleCollectionGroupName = "rcg-test",
        TlsRuleCollectionName = "rc-tls",
        FqdnRuleCollectionName = "rc-fqdn",
        TlsRuleCollectionPriority = 500,
        FqdnRuleCollectionPriority = 501,
        SourceAddresses = ["10.0.0.0/8"]
    };

    [Fact]
    public async Task DryRun_DoesNotExecuteCommands()
    {
        var executed = new List<string>();
        var deployer = new FirewallPolicyDeployer(
            CreateTestConfig(),
            dryRun: true,
            executeCommand: (prog, args) =>
            {
                executed.Add($"{prog} {args}");
                return Task.FromResult((0, "ok", ""));
            });

        var fqdns = new List<string> { "github.com", "objects.githubusercontent.com" };
        var result = await deployer.DeployAsync(fqdns);

        result.DryRun.Should().BeTrue();
        result.Commands.Should().HaveCount(3); // rcg create + tls + fqdn
        result.ScriptContent.Should().NotBeNullOrEmpty();
        result.ScriptContent.Should().Contain("github.com");
        result.ScriptContent.Should().Contain("objects.githubusercontent.com");
        executed.Should().BeEmpty(); // no actual execution
    }

    [Fact]
    public async Task Deploy_ExecutesThreeCommands()
    {
        var executed = new List<string>();
        var deployer = new FirewallPolicyDeployer(
            CreateTestConfig(),
            dryRun: false,
            executeCommand: (prog, args) =>
            {
                executed.Add($"{prog} {args}");
                return Task.FromResult((0, "ok", ""));
            });

        var fqdns = new List<string> { "github.com" };
        var result = await deployer.DeployAsync(fqdns);

        result.Success.Should().BeTrue();
        executed.Should().HaveCount(3);
        executed[0].Should().Contain("rule-collection-group create");
        executed[1].Should().Contain("rc-tls");
        executed[1].Should().Contain("enable-tls-inspection true");
        executed[2].Should().Contain("rc-fqdn");
        executed[2].Should().Contain("enable-tls-inspection false");
    }

    [Fact]
    public async Task Deploy_StopsOnFailure()
    {
        var callCount = 0;
        var deployer = new FirewallPolicyDeployer(
            CreateTestConfig(),
            dryRun: false,
            executeCommand: (prog, args) =>
            {
                callCount++;
                if (callCount == 2) return Task.FromResult((1, "", "error"));
                return Task.FromResult((0, "ok", ""));
            });

        var result = await deployer.DeployAsync(["github.com"]);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("failed");
        callCount.Should().Be(2); // stopped at second command
    }

    [Fact]
    public async Task Deploy_EmptyFqdns_Skips()
    {
        var deployer = new FirewallPolicyDeployer(CreateTestConfig(), dryRun: false);
        var result = await deployer.DeployAsync([]);
        result.Message.Should().Contain("No FQDNs");
    }

    [Fact]
    public async Task DryRun_ScriptContainsBothRuleCollections()
    {
        var deployer = new FirewallPolicyDeployer(
            CreateTestConfig(),
            dryRun: true,
            executeCommand: (_, _) => Task.FromResult((0, "", "")));

        var result = await deployer.DeployAsync(["cdn.example.com", "dl.example.com"]);

        result.ScriptContent.Should().Contain("rc-tls");
        result.ScriptContent.Should().Contain("rc-fqdn");
        result.ScriptContent.Should().Contain("cdn.example.com");
        result.ScriptContent.Should().Contain("dl.example.com");
        result.ScriptContent.Should().Contain("set -euo pipefail");
    }

    [Fact]
    public async Task Deploy_CommandsContainSourceAddresses()
    {
        var executed = new List<string>();
        var deployer = new FirewallPolicyDeployer(
            CreateTestConfig(),
            dryRun: false,
            executeCommand: (prog, args) =>
            {
                executed.Add(args);
                return Task.FromResult((0, "ok", ""));
            });

        await deployer.DeployAsync(["test.com"]);

        executed[1].Should().Contain("10.0.0.0/8");
        executed[2].Should().Contain("10.0.0.0/8");
    }
}
