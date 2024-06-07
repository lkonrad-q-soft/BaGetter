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
    /// The v2 index that will be mirrored.
    /// </summary>
    public Uri V2PackageSource { get; set; }

    /// <summary>
    /// The v3 indexes that are mirrored.
    /// </summary>
    public Uri[] V3PackageSources { get; set; }

    /// <summary>
    /// Whether or not the package source is a v2 package source feed.
    /// </summary>
    public bool Legacy { get; set; }

    /// <summary>
    /// The time before a download from the package source times out.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PackageDownloadTimeoutSeconds { get; set; } = 600;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Enabled && V2PackageSource == null)
        {
            yield return new ValidationResult(
                $"The {nameof(V2PackageSource)} configuration is required if mirroring is enabled",
                new[] { nameof(V2PackageSource) });
        }

        if (Enabled && !Legacy && (V3PackageSources == null || V3PackageSources == Array.Empty<Uri>()))
        {
            yield return new ValidationResult(
                $"The {nameof(V3PackageSources)} configuration is required if mirroring is enabled",
                new[] { nameof(V3PackageSources) });
        }
    }
}
