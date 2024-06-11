using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BaGetter.Core;

public class MirrorOptions : IValidatableObject
{
    /// <summary>
    /// If true, packages that aren't found locally will be indexed
    /// using the upstream source.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The v3 index that will be mirrored.
    /// </summary>
    public Uri PackageSource { get; set; }

    /// <summary>
    /// Whether or not the package source is a v2 package source feed.
    /// </summary>
    public bool Legacy { get; set; }

    /// <summary>
    /// The time before a download from the package source times out.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PackageDownloadTimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// The sources that will be mirrored.
    /// </summary>
    public HashSet<MirrorSource> Sources { get; set; } = new();

    /// <summary>
    /// Whether or not the mirror has multiple sources.
    /// </summary>
    public bool HasMultipleSources => Sources is not null && Sources.Count > 0;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (!HasMultipleSources)
        {
            // roll back to old config
            if (PackageSource is null)
            {
                yield return new ValidationResult(
                    $"The {nameof(PackageSource)} configuration is required if mirroring is enabled",
                    new[] { nameof(PackageSource) });
            }
        }
        else
        {
            // validate each source in the new list
            foreach (var source in Sources)
            {
                if (source.PackageSource is null)
                {
                    yield return new ValidationResult(
                        "Each source must have a valid URL defined",
                        new[] { nameof(source.PackageSource) });
                }
            }
        }
    }
}

public class MirrorSource
{
    public Uri PackageSource { get; set; }
    public bool Legacy { get; set; }

    public override bool Equals(object obj)
    {
        // Check for null and compare run-time types.
        if (obj == null || !this.GetType().Equals(obj.GetType()))
        {
            return false;
        }
        else
        {
            MirrorSource ms = (MirrorSource)obj;
            return (PackageSource.Equals(ms.PackageSource)) && (Legacy == ms.Legacy);
        }
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(PackageSource, Legacy);
    }
}
