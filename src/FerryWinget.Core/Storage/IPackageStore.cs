namespace FerryWinget.Core.Storage;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FerryWinget.Core.Models;

public interface IPackageStore
{
    Task SaveManifestAsync(string packageId, string version, string yamlContent, CancellationToken ct = default);
    Task SaveInstallerAsync(string packageId, string version, string architecture, string fileName, byte[] data, CancellationToken ct = default);
    Task<string?> LoadManifestAsync(string packageId, string version, CancellationToken ct = default);
    Task<byte[]?> LoadInstallerAsync(string packageId, string version, string architecture, string fileName, CancellationToken ct = default);
    Task<bool> InstallerExistsAsync(string packageId, string version, string architecture, string fileName, CancellationToken ct = default);
    IEnumerable<string> ListPackageIds();
    IEnumerable<string> ListVersions(string packageId);
    Task DeleteVersionAsync(string packageId, string version, CancellationToken ct = default);
}
