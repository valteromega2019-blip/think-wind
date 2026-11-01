using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace OrbyLauncher.Services;
internal sealed record ReleaseVersion(BigInteger Major, BigInteger Minor, BigInteger Patch, string[] Pre) : IComparable<ReleaseVersion>
{
    internal static ReleaseVersion? Parse(string value)
    {
        var match = Regex.Match(value, @"^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        string[] pre = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : Array.Empty<string>();
        if (pre.Any(p => p.Length == 0 || (p.All(char.IsDigit) && p.Length > 1 && p[0] == '0'))) return null;
        return new(BigInteger.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), BigInteger.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), BigInteger.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), pre);
    }
    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        int result = Major.CompareTo(other.Major); if (result != 0) return result;
        result = Minor.CompareTo(other.Minor); if (result != 0) return result;
        result = Patch.CompareTo(other.Patch); if (result != 0) return result;
        if (Pre.Length == 0 || other.Pre.Length == 0) return (Pre.Length == 0 ? 1 : 0).CompareTo(other.Pre.Length == 0 ? 1 : 0);
        for (int i = 0; i < Math.Min(Pre.Length, other.Pre.Length); i++)
        {
            bool a = Pre[i].All(char.IsDigit), b = other.Pre[i].All(char.IsDigit);
            BigInteger av = a ? BigInteger.Parse(Pre[i], CultureInfo.InvariantCulture) : 0, bv = b ? BigInteger.Parse(other.Pre[i], CultureInfo.InvariantCulture) : 0;
            result = a && b ? av.CompareTo(bv) : a != b ? (a ? -1 : 1) : StringComparer.Ordinal.Compare(Pre[i], other.Pre[i]);
            if (result != 0) return result;
        }
        return Pre.Length.CompareTo(other.Pre.Length);
    }
}
