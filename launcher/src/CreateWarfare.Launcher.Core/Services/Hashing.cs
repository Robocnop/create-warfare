using System.Security.Cryptography;

namespace CreateWarfare.Launcher.Core.Services;

public static class Hashing
{
    public static string Sha1(string path) => Hash(path, SHA1.Create());
    public static string Sha256(string path) => Hash(path, SHA256.Create());

    static string Hash(string path, HashAlgorithm algo)
    {
        using (algo)
        using (var s = File.OpenRead(path))
            return Convert.ToHexString(algo.ComputeHash(s)).ToLowerInvariant();
    }

    public static bool Matches(string path, string expected, bool sha1) =>
        File.Exists(path) && string.Equals(sha1 ? Sha1(path) : Sha256(path), expected, StringComparison.OrdinalIgnoreCase);
}
