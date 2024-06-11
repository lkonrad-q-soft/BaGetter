using Microsoft.Extensions.Options;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BaGetter.Core;
internal class MultiFeedUpstreamClient : IUpstreamClient
{
    private readonly List<IUpstreamClient> _clients;

    public MultiFeedUpstreamClient(IOptionsSnapshot<MirrorOptions> options, List<IUpstreamClient> clients)
    {
        ArgumentNullException.ThrowIfNull(clients);

        if (!options.Value.HasMultipleSources && clients.Count == 0)
        {
            throw new ArgumentException("At least one upstream client must be provided.");
        }

        _clients = clients;
    }

    public async Task<IReadOnlyList<NuGetVersion>> ListPackageVersionsAsync(string id, CancellationToken cancellationToken)
    {
        var versions = new List<NuGetVersion>();

        foreach (var client in _clients)
        {
            var clientVersions = await client.ListPackageVersionsAsync(id, cancellationToken);
            versions.AddRange(clientVersions);
        }

        return versions.DistinctBy(c => c.OriginalVersion).ToList();
    }

    public async Task<IReadOnlyList<Package>> ListPackagesAsync(string id, CancellationToken cancellationToken)
    {
        var packages = new List<Package>();

        foreach (var client in _clients)
        {
            var clientPackages = await client.ListPackagesAsync(id, cancellationToken);
            packages.AddRange(clientPackages);
        }

        return packages.DistinctBy(p => p.Version).ToList();
    }

    public async Task<Stream> DownloadPackageOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
    {
        foreach (var client in _clients)
        {
            var packageStream = await client.DownloadPackageOrNullAsync(id, version, cancellationToken);
            if (packageStream != null)
            {
                return packageStream;
            }
        }

        return null;
    }
}
