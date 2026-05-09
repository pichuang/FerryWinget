namespace FerryWinget.Server.Tests;

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FerryWinget.Core.Configuration;
using FerryWinget.Core.Storage;
using FerryWinget.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shared test fixture that sets up a WebApplicationFactory with test data in ./tmp_download.
/// </summary>
public sealed class TestServerFixture : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    public HttpClient Client { get; }

    private static readonly string TestDataRoot = Path.Combine(".", "tmp_download", "server-test");
    private static readonly string ConfigPath = Path.Combine(TestDataRoot, "config.yaml");

    public TestServerFixture()
    {
        // Clean up previous test data
        if (Directory.Exists(TestDataRoot))
            Directory.Delete(TestDataRoot, true);
        Directory.CreateDirectory(TestDataRoot);

        // Create test config
        var configYaml = $"""
            source:
              github_repo: "microsoft/winget-pkgs"
            storage:
              root_path: "{TestDataRoot.Replace("\\", "/")}/mirror-data"
              packages_dir: "packages"
              reports_dir: "reports"
            filtering:
              allowlist:
                enabled: true
                publishers: []
                packages:
                  - "GitHub.*"
              blocklist:
                enabled: true
                publishers: []
                packages:
                  - "Google.*"
            server:
              port: 0
              source_identifier: "FerryWinget"
              supported_api_versions:
                - "1.4.0"
                - "1.7.0"
                - "1.9.0"
            timezone: "Asia/Taipei"
            """;
        File.WriteAllText(ConfigPath, configYaml);

        // Seed test data
        var storageRoot = Path.Combine(TestDataRoot, "mirror-data");
        var store = new FileSystemPackageStore(storageRoot);
        SeedTestData(store).Wait();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace config
                    services.AddSingleton(ConfigLoader.LoadFromString(configYaml));
                    services.AddSingleton(ConfigLoader.LoadFromString(configYaml).Server);

                    // Replace store
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPackageStore));
                    if (descriptor is not null) services.Remove(descriptor);
                    services.AddSingleton<IPackageStore>(new FileSystemPackageStore(storageRoot));
                });
            });

        Client = _factory.CreateClient();
    }

    private static async Task SeedTestData(FileSystemPackageStore store)
    {
        // GitHub.TestPkg with 2 versions
        var manifest1 = """
            PackageIdentifier: GitHub.TestPkg
            PackageVersion: 1.0.0
            Installers:
              - Architecture: x64
                InstallerUrl: https://github.com/test/releases/download/v1.0.0/setup.exe
                InstallerSha256: ABC123
                InstallerType: exe
                Scope: machine
                ProductCode: '{TEST-1000}'
            """;
        await store.SaveManifestAsync("GitHub.TestPkg", "1.0.0", manifest1);
        await store.SaveInstallerAsync("GitHub.TestPkg", "1.0.0", "x64", "setup.exe", new byte[] { 0x4D, 0x5A });

        var manifest2 = """
            PackageIdentifier: GitHub.TestPkg
            PackageVersion: 2.0.0
            Installers:
              - Architecture: x64
                InstallerUrl: https://github.com/test/releases/download/v2.0.0/setup.exe
                InstallerSha256: DEF456
                InstallerType: exe
            """;
        await store.SaveManifestAsync("GitHub.TestPkg", "2.0.0", manifest2);
        await store.SaveInstallerAsync("GitHub.TestPkg", "2.0.0", "x64", "setup.exe", new byte[] { 0x4D, 0x5A });

        // GitHub.AnotherPkg
        var manifest3 = """
            PackageIdentifier: GitHub.AnotherPkg
            PackageVersion: 3.0.0
            Installers:
              - Architecture: x64
                InstallerUrl: https://github.com/another/releases/download/v3.0.0/installer.msi
                InstallerSha256: GHI789
                InstallerType: msi
            """;
        await store.SaveManifestAsync("GitHub.AnotherPkg", "3.0.0", manifest3);
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        if (Directory.Exists(TestDataRoot))
            Directory.Delete(TestDataRoot, true);
    }
}
