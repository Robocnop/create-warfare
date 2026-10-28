namespace CreateWarfare.Launcher.Core.Services;

/// <summary>Chemins relatifs à l'instance, toujours avec des / et comparés sans tenir compte de la casse (Windows).</summary>
public static class PathRules
{
    public static string Normalize(string rel) => rel.Replace('\\', '/').TrimStart('/');

    /// <summary>Une entrée finie par / protège tout le dossier, sinon le fichier exact.</summary>
    public static bool IsProtected(string rel, IEnumerable<string> rules)
    {
        rel = Normalize(rel);
        foreach (var raw in rules)
        {
            var r = Normalize(raw);
            if (r.EndsWith('/') ? rel.StartsWith(r, StringComparison.OrdinalIgnoreCase)
                                : rel.Equals(r, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Refuse les chemins qui sortiraient de l'instance (zip malveillant, « ..\ »).</summary>
    public static string Resolve(string root, string rel)
    {
        var full = Path.GetFullPath(Path.Combine(root, Normalize(rel)));
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Chemin hors de l'instance : {rel}");
        return full;
    }
}
