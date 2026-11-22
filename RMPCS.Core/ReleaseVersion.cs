using System.Text.RegularExpressions;

namespace RMPCS.Core;

public sealed record ReleaseVersion(int Major, int Minor, int Patch, string Preview) : IComparable<ReleaseVersion>
{
    private static readonly Regex Format = new(@"\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static bool TryParse(string? tag, out ReleaseVersion version)
    {
        version = null!;
        if (tag is null || tag.Length > 128) return false;
        var match = Format.Match(tag);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int major) || !int.TryParse(match.Groups[2].Value, out int minor) || !int.TryParse(match.Groups[3].Value, out int patch)) return false;
        string preview = match.Groups[4].Value;
        if (preview.Split('.').Any(x => x.Length > 1 && x[0] == '0' && x.All(char.IsAsciiDigit))) return false;
        version = new(major, minor, patch, preview); return true;
    }
    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        int result = Major.CompareTo(other.Major); if (result != 0) return result;
        result = Minor.CompareTo(other.Minor); if (result != 0) return result;
        result = Patch.CompareTo(other.Patch); if (result != 0) return result;
        if (Preview.Length == 0 || other.Preview.Length == 0) return Preview.Length == other.Preview.Length ? 0 : Preview.Length == 0 ? 1 : -1;
        var left = Preview.Split('.'); var right = other.Preview.Split('.');
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            bool a = left[i].All(char.IsAsciiDigit), b = right[i].All(char.IsAsciiDigit);
            result = a && b ? left[i].Length != right[i].Length ? left[i].Length.CompareTo(right[i].Length) : string.CompareOrdinal(left[i], right[i]) : a != b ? a ? -1 : 1 : string.CompareOrdinal(left[i], right[i]);
            if (result != 0) return result;
        }
        return left.Length.CompareTo(right.Length);
    }
}
