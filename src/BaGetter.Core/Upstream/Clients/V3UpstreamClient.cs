using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Protocol;
using BaGetter.Protocol.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NuGet.Versioning;

namespace BaGetter.Core;

/// <summary>
/// The mirroring client for a NuGet server that uses the V3 protocol.
/// </summary>
public class V3UpstreamClient : IUpstreamClient
{
    private readonly ILogger<V3UpstreamClient> _logger;
    private readonly List<NuGetClient> v3Clients;
    private static readonly char[] AuthorSeparator = [',', ';', '\t', '\n', '\r'];
    private static readonly char[] TagSeparator = [' '];

    public V3UpstreamClient(ILogger<V3UpstreamClient> logger, IOptionsSnapshot<MirrorOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value.V3PackageSources);

        v3Clients = CreateClients(options.Value.V3PackageSources.ToArray());
    }

    public V3UpstreamClient(
        ILogger<V3UpstreamClient> logger,
        IOptionsSnapshot<MirrorOptions> options,
        NuGetClient client = null
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value.V3PackageSources);

        v3Clients = CreateClients(options.Value.V3PackageSources.ToArray());

        if (client is not null)
        {
            v3Clients.Add(client);
        }
    }

    private static List<NuGetClient> CreateClients(Uri[] uris)
    {
        ArgumentNullException.ThrowIfNull(uris);

        var clients = new List<NuGetClient>();
        foreach (var uri in uris)
        {
            clients.Add(new NuGetClient(uri.ToString()));
        }

        return clients;
    }

    public async Task<Stream> DownloadPackageOrNullAsync(
        string id,
        NuGetVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var client in v3Clients)
            {
                // Checks whether the package can be found in a client...
                var result = await CheckIfPackageExists(client, id, version, cancellationToken);

                if (!result.IsSuccess || !result.ResultValue)
                {
                    continue;
                }

                using (var downloadStream = await client.DownloadPackageAsync(id, version, cancellationToken))
                {
                    return await downloadStream.AsTemporaryFileStreamAsync(cancellationToken);
                }
            }

            return null;
        }
        catch (PackageNotFoundException)
        {
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to download {PackageId} {PackageVersion} from upstream", id, version);
            return null;
        }
    }

    private static async Task<ResultHandler<bool>> CheckIfPackageExists(
        NuGetClient client,
        string id,
        NuGetVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await client.ExistsAsync(id, version, cancellationToken);

            return new ResultHandler<bool>
            {
                ResultValue = result
            };
        }
        catch (PackageNotFoundException)
        {
            return new ResultHandler<bool>
            {
                ResultValue = false
            };
        }
        catch (Exception e)
        {
            return new ResultHandler<bool>
            {
                ThronException = e,
                ResultValue = false
            };
        }
    }

    public async Task<IReadOnlyList<Package>> ListPackagesAsync(
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<Package>();
            foreach (var client in v3Clients)
            {
                var listPkgsResult = await ListPackagesInternalAsync(client, id, cancellationToken);

                if (listPkgsResult.IsSuccess && listPkgsResult.ResultValue.Any())
                {
                    result.AddRange(listPkgsResult.ResultValue);
                }
            }

            return result.DistinctBy(c => c.Version).ToList();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to mirror {PackageId}'s upstream metadata", id);
            return new List<Package>();
        }
    }

    private async Task<ResultHandler<IReadOnlyList<Package>>> ListPackagesInternalAsync(
        NuGetClient client,
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var list = await client.GetPackageMetadataAsync(id, cancellationToken);

            return new ResultHandler<IReadOnlyList<Package>>
            {
                ResultValue = list.Select(ToPackage).ToList()
            };

        }
        catch (Exception e)
        {
            return new ResultHandler<IReadOnlyList<Package>>
            {
                ThronException = e,
                ResultValue = new List<Package>()
            };
        }
    }

    public async Task<IReadOnlyList<NuGetVersion>> ListPackageVersionsAsync(
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<NuGetVersion>();
            foreach (var client in v3Clients)
            {
                var listPkgVersionsResult = await ListPackageVersionsInternalAsync(client, id, cancellationToken);

                if (listPkgVersionsResult.IsSuccess && listPkgVersionsResult.ResultValue.Any())
                {
                    result.AddRange(listPkgVersionsResult.ResultValue);
                }
            }

            result = result.DistinctBy(c => c.OriginalVersion).ToList();
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to mirror {PackageId}'s upstream versions", id);
            return new List<NuGetVersion>();
        }
    }

    private static async Task<ResultHandler<IReadOnlyList<NuGetVersion>>> ListPackageVersionsInternalAsync(
        NuGetClient client,
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var list = await client.ListPackageVersionsAsync(id, includeUnlisted: true, cancellationToken);

            return new ResultHandler<IReadOnlyList<NuGetVersion>>
            {
                ResultValue = list
            };
        }
        catch (Exception e)
        {
            return new ResultHandler<IReadOnlyList<NuGetVersion>>
            {
                ThronException = e,
                ResultValue = new List<NuGetVersion>()
            };
        }
    }

    private sealed class ResultHandler<T>
    {
        public T ResultValue { get; set; }
        public Exception ThronException { get; set; } = null;
        public bool IsSuccess => ThronException == null;
    }

    private Package ToPackage(PackageMetadata metadata)
    {
        var version = metadata.ParseVersion();

        return new Package
        {
            Id = metadata.PackageId,
            Version = version,
            Authors = ParseAuthors(metadata.Authors),
            Description = metadata.Description,
            Downloads = 0,
            HasReadme = false,
            IsPrerelease = version.IsPrerelease,
            Language = metadata.Language,
            Listed = metadata.IsListed(),
            MinClientVersion = metadata.MinClientVersion,
            Published = metadata.Published.UtcDateTime,
            RequireLicenseAcceptance = metadata.RequireLicenseAcceptance,
            Summary = metadata.Summary,
            Title = metadata.Title,
            IconUrl = ParseUri(metadata.IconUrl),
            LicenseUrl = ParseUri(metadata.LicenseUrl),
            ProjectUrl = ParseUri(metadata.ProjectUrl),
            PackageTypes = new List<PackageType>(),
            RepositoryUrl = null,
            RepositoryType = null,
            SemVerLevel = version.IsSemVer2 ? SemVerLevel.SemVer2 : SemVerLevel.Unknown,
            Tags = ParseTags(metadata.Tags),

            Dependencies = ToDependencies(metadata)
        };
    }

    private static Uri ParseUri(string uriString)
    {
        if (uriString == null) return null;

        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri;
    }

    private static string[] ParseAuthors(string authors)
    {
        if (string.IsNullOrEmpty(authors))
        {
            return Array.Empty<string>();
        }

        return authors.Split(AuthorSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string[] ParseTags(IEnumerable<string> tags)
    {
        if (tags is null)
        {
            return Array.Empty<string>();
        }

        return tags
            .SelectMany(t => t.Split(TagSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
    }

    private List<PackageDependency> ToDependencies(PackageMetadata package)
    {
        if ((package.DependencyGroups?.Count ?? 0) == 0)
        {
            return new List<PackageDependency>();
        }

        return package.DependencyGroups
            .SelectMany(ToDependencies)
            .ToList();
    }

    private IEnumerable<PackageDependency> ToDependencies(DependencyGroupItem group)
    {
        // BaGetter stores a dependency group with no dependencies as a package dependency
        // with no package id nor package version.
        if ((group.Dependencies?.Count ?? 0) == 0)
        {
            return new[]
            {
                new PackageDependency
                {
                    Id = null,
                    VersionRange = null,
                    TargetFramework = group.TargetFramework,
                }
            };
        }

        return group.Dependencies.Select(d => new PackageDependency
        {
            Id = d.Id,
            VersionRange = d.Range,
            TargetFramework = group.TargetFramework,
        });
    }
}
