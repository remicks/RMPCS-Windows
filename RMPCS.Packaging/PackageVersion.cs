using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

static class PackageVersion
{
    internal const string UpgradeCode = "{C19B49F3-55BC-4427-AE35-3E1E27B732A5}";
    internal static Version Parse(string text)
    {
        if (!Regex.IsMatch(text, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z") ||
            !Version.TryParse(text, out var version) || version.Major > 255 || version.Minor > 255 || version.Build > 65535)
            throw new InvalidDataException("Use a canonical MSI version: major.minor.build within 255.255.65535.");
        return version;
    }
    internal static string FromApp(string app)
    {
        string assembly = Path.Combine(Path.GetFullPath(app), "RMPCS.dll");
        var identity = AssemblyName.GetAssemblyName(assembly); // Metadata only; never execute the payload.
        var value = identity.Version ?? throw new InvalidDataException("Application version metadata is missing.");
        if (identity.Name != "RMPCS" || value.Revision != 0) throw new InvalidDataException("Unexpected application identity or fourth version field.");
        string version = value.ToString(3); Parse(version);
        var file = FileVersionInfo.GetVersionInfo(assembly);
        if (file.FileVersion != version + ".0" || file.ProductVersion?.Split('+')[0] != version)
            throw new InvalidDataException("Application assembly, file and informational versions disagree.");
        return version;
    }
    internal static void RequireExpected(string app, string expected)
    {
        Parse(expected);
        if (FromApp(app) != expected) throw new InvalidDataException("Published application version differs from the requested release version.");
    }
}
