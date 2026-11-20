using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace RMPCS.Bootstrap
{
    public static class BootstrapPolicy
    {
        public const string RuntimeUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.15/windowsdesktop-runtime-10.0.15-win-x64.exe";
        public const string RuntimeSha512 = "f838960962172f1a769c49fa600e6f60f56f8d08a28e3cb878890ffc2e3a7a2e1b65697588e23efa767b8b8e8523ec01f55e875120507dde16d9a0869102dab3";
        public static bool CompatibleVersion(string value)
        { Version version; return Version.TryParse(value, out version) && version.Major == 10 && version.Minor == 0; }
        public static Dictionary<string, string> ParseManifest(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = line.Trim().Split('|');
                if (pair.Length != 2 || pair[0].Length == 0 || pair[0] == "." || pair[0] == ".." || Path.GetFileName(pair[0]) != pair[0] || pair[0].IndexOfAny(new[] {'/', '\\', ':'}) >= 0 || pair[1].Length != 64 || !IsHex(pair[1])) throw new InvalidDataException("Invalid payload manifest.");
                result.Add(pair[0], pair[1].ToLowerInvariant());
            }
            if (result.Count == 0) throw new InvalidDataException("Empty payload manifest.");
            return result;
        }
        public static bool IsHex(string text)
        { foreach (char c in text) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false; return true; }
        public static string Hash(Stream stream, bool sha512)
        { using (HashAlgorithm hash = sha512 ? (HashAlgorithm)SHA512.Create() : SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        public static bool AcceptedExit(int exit) { return exit == 0 || exit == 3010; }
    }
}
