using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace BaGetter.Core;

public class MirrorOptions : IValidatableObject
{
    private Uri _packageSource;
    private bool _legacy;

    /// <summary>
    /// If true, packages that aren't found locally will be indexed
    /// using the upstream source.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The v3 index that will be mirrored.
    /// </summary>
    public Uri PackageSource
    {
        get => _packageSource;
        set
        {
            _packageSource = value;
            UpdateSources();
        }
    }

    /// <summary>
    /// Whether or not the package source is a v2 package source feed.
    /// </summary>
    public bool Legacy
    {
        get => _legacy;
        set
        {
            _legacy = value;
            UpdateSources();
        }
    }

    /// <summary>
    /// The time before a download from the package source times out.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PackageDownloadTimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// The sources that will be mirrored.
    /// </summary>
    public HashSet<MirrorSource> Sources { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

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

    private void UpdateSources()
    {
        if (_packageSource is null)
            return;

        Sources = [new MirrorSource { PackageSource = PackageSource, Legacy = Legacy }]; 
    }
}

public class MirrorSource
{
    public Uri PackageSource { get; set; }
    public bool Legacy { get; set; }

    public override bool Equals(object obj)
    {
        // Check for null and compare run-time types.
        if (obj is null || !GetType().Equals(obj.GetType()))
        {
            return false;
        }
        else
        {
            var ms = (MirrorSource)obj;
            return (PackageSource.Equals(ms.PackageSource)) && (Legacy == ms.Legacy);
        }
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(PackageSource, Legacy);
    }
}
