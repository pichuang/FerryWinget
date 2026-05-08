namespace FerryWinget.Server.Controllers;

using System.IO;
using System.Linq;
using FerryWinget.Core.Configuration;
using FerryWinget.Core.Storage;
using FerryWinget.Server.Models;
using FerryWinget.Server.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public sealed class PackageManifestsController : ControllerBase
{
    private readonly PackageIndexService _indexService;
    private readonly IPackageStore _store;
    private readonly ServerConfig _serverConfig;

    public PackageManifestsController(
        PackageIndexService indexService,
        IPackageStore store,
        ServerConfig serverConfig)
    {
        _indexService = indexService;
        _store = store;
        _serverConfig = serverConfig;
    }

    [HttpGet("packageManifests/{packageIdentifier}")]
    public ActionResult<PackageManifestResponse> GetManifest(
        string packageIdentifier,
        [FromQuery(Name = "Version")] string? version = null)
    {
        var pkg = _indexService.GetPackage(packageIdentifier);
        if (pkg is null)
            return NotFound();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var versions = pkg.Versions.AsEnumerable();
        if (!string.IsNullOrEmpty(version))
            versions = versions.Where(v => v.PackageVersion == version);

        var response = new PackageManifestResponse
        {
            Data = new PackageManifestData
            {
                PackageIdentifier = pkg.PackageIdentifier,
                Versions = versions.Select(v => new PackageManifestVersion
                {
                    PackageVersion = v.PackageVersion,
                    DefaultLocale = new DefaultLocaleData
                    {
                        Publisher = pkg.Publisher,
                        PackageName = pkg.PackageName,
                        License = "Unknown",
                        ShortDescription = $"{pkg.PackageName} by {pkg.Publisher}"
                    },
                    Installers = v.Installers.Select(i => new InstallerData
                    {
                        Architecture = i.Architecture,
                        InstallerSha256 = i.InstallerSha256,
                        InstallerUrl = RewriteInstallerUrl(baseUrl, pkg.PackageIdentifier, v.PackageVersion, i),
                        InstallerType = i.InstallerType,
                        Scope = i.Scope,
                        ProductCode = i.ProductCode
                    }).ToList()
                }).ToList()
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// Serves the actual installer binary file.
    /// </summary>
    [HttpGet("installers/{packageIdentifier}/{version}/{architecture}/{fileName}")]
    public async Task<IActionResult> GetInstaller(
        string packageIdentifier, string version, string architecture, string fileName)
    {
        var data = await _store.LoadInstallerAsync(packageIdentifier, version, architecture, fileName);
        if (data is null)
            return NotFound();

        var contentType = fileName.EndsWith(".msi", System.StringComparison.OrdinalIgnoreCase)
            ? "application/x-msi"
            : "application/octet-stream";

        return File(data, contentType, fileName);
    }

    private static string RewriteInstallerUrl(
        string baseUrl, string packageId, string version, IndexedInstaller installer)
    {
        var originalUrl = installer.InstallerUrl;
        if (string.IsNullOrEmpty(originalUrl)) return originalUrl;

        var fileName = Path.GetFileName(new System.Uri(originalUrl).LocalPath);
        return $"{baseUrl}/api/installers/{packageId}/{version}/{installer.Architecture}/{fileName}";
    }
}
