namespace FerryWinget.Server.Tests;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FerryWinget.Core.Storage;
using FerryWinget.Server.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class ServerIntegrationTests : IClassFixture<TestServerFixture>, IDisposable
{
    private readonly HttpClient _client;
    private readonly TestServerFixture _fixture;

    public ServerIntegrationTests(TestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    public void Dispose() { }

    [Fact]
    public async Task GetInformation_ReturnsValidResponse()
    {
        var response = await _client.GetAsync("/api/information");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<InformationResponse>();
        body.Should().NotBeNull();
        body!.Data.SourceIdentifier.Should().Be("FerryWinget");
        body.Data.ServerSupportedVersions.Should().Contain("1.4.0");
        body.Data.ServerSupportedVersions.Should().Contain("1.7.0");
        body.Data.ServerSupportedVersions.Should().Contain("1.9.0");
        body.Data.Authentication.Should().BeNull();
    }

    [Fact]
    public async Task ManifestSearch_EmptyQuery_ReturnsAllPackages()
    {
        var request = new ManifestSearchRequest
        {
            MaximumResults = 100,
            FetchAllManifests = true
        };

        var response = await _client.PostAsJsonAsync("/api/manifestSearch", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ManifestSearchResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body.Should().NotBeNull();
        body!.Data.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ManifestSearch_ByKeyword_FiltersResults()
    {
        var request = new ManifestSearchRequest
        {
            Query = new SearchQuery { KeyWord = "GitHub.TestPkg", MatchType = "Substring" }
        };

        var response = await _client.PostAsJsonAsync("/api/manifestSearch", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ManifestSearchResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body!.Data.Should().AllSatisfy(r =>
            r.PackageIdentifier.Should().Contain("GitHub.TestPkg"));
    }

    [Fact]
    public async Task ManifestSearch_ByFilter_ExactMatch()
    {
        var request = new ManifestSearchRequest
        {
            Filters =
            [
                new()
                {
                    PackageMatchField = "PackageIdentifier",
                    RequestMatch = new() { KeyWord = "GitHub.TestPkg", MatchType = "CaseInsensitive" }
                }
            ]
        };

        var response = await _client.PostAsJsonAsync("/api/manifestSearch", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ManifestSearchResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body!.Data.Should().HaveCount(1);
        body.Data[0].PackageIdentifier.Should().Be("GitHub.TestPkg");
    }

    [Fact]
    public async Task GetPackageManifest_ReturnsFullManifest()
    {
        var response = await _client.GetAsync("/api/packageManifests/GitHub.TestPkg");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PackageManifestResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body.Should().NotBeNull();
        body!.Data.Should().NotBeNull();
        body.Data!.PackageIdentifier.Should().Be("GitHub.TestPkg");
        body.Data.Versions.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GetPackageManifest_VersionFilter_Works()
    {
        var response = await _client.GetAsync("/api/packageManifests/GitHub.TestPkg?Version=1.0.0");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PackageManifestResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body!.Data!.Versions.Should().HaveCount(1);
        body.Data.Versions[0].PackageVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task GetPackageManifest_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/packageManifests/Nonexistent.Package");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPackageManifest_InstallerUrlRewritten()
    {
        var response = await _client.GetAsync("/api/packageManifests/GitHub.TestPkg");
        var body = await response.Content.ReadFromJsonAsync<PackageManifestResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var installer = body!.Data!.Versions[0].Installers[0];
        installer.InstallerUrl.Should().Contain("/api/installers/GitHub.TestPkg/");
        installer.InstallerUrl.Should().NotContain("github.com");
    }

    [Fact]
    public async Task GetInstaller_ReturnsFile()
    {
        var response = await _client.GetAsync("/api/installers/GitHub.TestPkg/1.0.0/x64/setup.exe");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await response.Content.ReadAsByteArrayAsync();
        data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetInstaller_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/installers/Missing.Pkg/1.0.0/x64/setup.exe");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
