using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaGetter.Core;

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

