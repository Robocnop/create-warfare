using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CreateWarfare.Launcher.Core.Services;

// Informations affichées par le launcher 1.2 : état du serveur, actualités, contenu distant, RAM.

// ============================================================ Serveur

/// <summary>État d'un serveur Minecraft, lu par le protocole « Server List Ping » (celui de la liste Multijoueur).</summary>
public sealed record ServerStatus(bool Online, int Players, int MaxPlayers, string Version, string Motd, int PingMs, string? Error = null)
{
    public static ServerStatus Offline(string error) => new(false, 0, 0, "", "", -1, error);
}

public static class ServerPing
{
    public const string DefaultHost = "jeu.robocnop.be";
    public const int DefaultPort = 27018;

    /// <summary>Interroge le serveur (handshake + status) sans jamais lever d'exception : un échec donne Offline.</summary>
    public static async Task<ServerStatus> QueryAsync(string host, int port, TimeSpan timeout, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            using var client = new TcpClient();
            var sw = Stopwatch.StartNew();
            await client.ConnectAsync(host, port, cts.Token);
            var pingMs = (int)sw.ElapsedMilliseconds;
            var s = client.GetStream();

            // Handshake (protocole -1 : accepté par toutes les versions), puis demande de statut
            var hs = new List<byte>();
            WriteVarInt(hs, 0x00);
            WriteVarInt(hs, -1);
            var h = Encoding.UTF8.GetBytes(host);
            WriteVarInt(hs, h.Length);
            hs.AddRange(h);
            hs.Add((byte)(port >> 8));
            hs.Add((byte)port);
            WriteVarInt(hs, 1);
            await s.WriteAsync(Packet(hs), cts.Token);
            await s.WriteAsync(new byte[] { 0x01, 0x00 }, cts.Token);

            await ReadVarIntAsync(s, cts.Token); // longueur du paquet
            if (await ReadVarIntAsync(s, cts.Token) != 0x00) return ServerStatus.Offline("réponse inattendue");
            var len = await ReadVarIntAsync(s, cts.Token);
            if (len <= 0 || len > 1 << 20) return ServerStatus.Offline("réponse invalide");
            var buf = new byte[len];
            for (int read = 0; read < len;)
            {
                var n = await s.ReadAsync(buf.AsMemory(read, len - read), cts.Token);
                if (n == 0) throw new IOException("connexion fermée");
                read += n;
            }
            return Parse(Encoding.UTF8.GetString(buf), pingMs);
        }
        catch (Exception e) when (e is SocketException or IOException or OperationCanceledException or JsonException or InvalidDataException)
        {
            return ServerStatus.Offline(e is OperationCanceledException ? "délai dépassé" : e.Message);
        }
    }

    /// <summary>Lit la réponse JSON du serveur (joueurs, version, MOTD en texte ou en composant).</summary>
    public static ServerStatus Parse(string json, int pingMs)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        int online = 0, max = 0;
        if (r.TryGetProperty("players", out var p))
        {
            if (p.TryGetProperty("online", out var o)) online = o.GetInt32();
            if (p.TryGetProperty("max", out var m)) max = m.GetInt32();
        }
        var version = r.TryGetProperty("version", out var v) && v.TryGetProperty("name", out var vn) ? vn.GetString() ?? "" : "";
        var motd = r.TryGetProperty("description", out var d) ? FlattenText(d) : "";
        return new ServerStatus(true, online, max, version, StripFormatting(motd).Trim(), pingMs);
    }

    static string FlattenText(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString() ?? "",
        JsonValueKind.Object => (e.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "")
                                + (e.TryGetProperty("extra", out var x) && x.ValueKind == JsonValueKind.Array
                                    ? string.Concat(x.EnumerateArray().Select(FlattenText)) : ""),
        JsonValueKind.Array => string.Concat(e.EnumerateArray().Select(FlattenText)),
        _ => "",
    };

    /// <summary>Retire les codes de mise en forme Minecraft (caractère section suivi d'un code).</summary>
    public static string StripFormatting(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '§' && i + 1 < s.Length) { i++; continue; }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    static byte[] Packet(List<byte> body)
    {
        var p = new List<byte>();
        WriteVarInt(p, body.Count);
        p.AddRange(body);
        return p.ToArray();
    }

    public static void WriteVarInt(List<byte> b, int value)
    {
        var v = (uint)value;
        do
        {
            var temp = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) temp |= 0x80;
            b.Add(temp);
        } while (v != 0);
    }

    static async Task<int> ReadVarIntAsync(Stream s, CancellationToken ct)
    {
        int result = 0, shift = 0;
        var one = new byte[1];
        while (true)
        {
            if (await s.ReadAsync(one, ct) == 0) throw new IOException("connexion fermée");
            result |= (one[0] & 0x7F) << shift;
            if ((one[0] & 0x80) == 0) return result;
            shift += 7;
            if (shift > 35) throw new InvalidDataException("VarInt trop long");
        }
    }
}

// ============================================================ Actualités (changelog de version.json)

/// <summary>Une version du journal, découpée pour l'écran Actualités.</summary>
public sealed record ChangelogEntry(string Version, string Date, IReadOnlyList<ChangelogItem> Items);

/// <summary>Une ligne : catégorie déduite (ajout, correctif, retrait, modification), titre en gras et texte.</summary>
public sealed record ChangelogItem(string Category, string Title, string Text);

public static partial class ChangelogParser
{
    public const string Added = "added";
    public const string Fixed = "fixed";
    public const string Removed = "removed";
    public const string Changed = "changed";

    /// <summary>Découpe le champ changelog de version.json : sections « 1.8.0 — 2026-11-01 » puis lignes « - Titre : texte ».</summary>
    public static List<ChangelogEntry> Parse(string? changelog)
    {
        var entries = new List<ChangelogEntry>();
        if (string.IsNullOrWhiteSpace(changelog)) return entries;
        foreach (var block in Regex.Split(changelog.Replace("\r\n", "\n").Trim(), @"\n\s*\n(?=\d+\.\d+)"))
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0) continue;
            var head = Header().Match(lines[0]);
            var version = head.Success ? head.Groups["v"].Value : lines[0];
            var date = head.Success ? head.Groups["d"].Value : "";
            var items = new List<ChangelogItem>();
            foreach (var raw in lines.Skip(1))
            {
                if (!raw.StartsWith('-'))
                {
                    if (items.Count > 0) items[^1] = items[^1] with { Text = (items[^1].Text + " " + raw).Trim() };
                    continue;
                }
                var line = raw.TrimStart('-', ' ');
                var colon = line.IndexOf(" : ", StringComparison.Ordinal);
                var hasTitle = colon > 0 && colon < 90;
                var title = hasTitle ? line[..colon] : "";
                var text = hasTitle ? line[(colon + 3)..] : line;
                items.Add(new ChangelogItem(CategoryOf(title + " " + text), title, text));
            }
            entries.Add(new ChangelogEntry(version, date, items));
        }
        return entries;
    }

    /// <summary>Catégorie d'une ligne d'après ses mots (le journal est en français libre).</summary>
    public static string CategoryOf(string s)
    {
        var l = s.ToLowerInvariant();
        if (l.Contains("correctif") || l.Contains("corrig") || l.Contains("plantage") || l.Contains("répar")) return Fixed;
        if (l.Contains("retir") || l.Contains("supprim") || l.Contains("écarté")) return Removed;
        if (l.Contains("ajout") || l.Contains("nouve") || l.Contains("quêtes")) return Added;
        return Changed;
    }

    [GeneratedRegex(@"^(?<v>\d+(\.\d+)+\S*)\s*[—-]\s*(?<d>\d{4}-\d{2}-\d{2})")]
    private static partial Regex Header();
}

// ============================================================ Contenu distant (launcher-content.json)

/// <summary>
/// launcher-content.json, à la racine du dépôt : actualités, images du carrousel, liens, serveur.
/// Modifiable sans republier le launcher. Tous les champs sont facultatifs.
/// </summary>
public sealed class LauncherContent
{
    public List<NewsItem> News { get; set; } = new();
    public List<GalleryItem> Gallery { get; set; } = new();
    public ContentLinks Links { get; set; } = new();
    public ContentServer? Server { get; set; }

    public const string DefaultUrl = "https://raw.githubusercontent.com/Robocnop/create-warfare/master/launcher-content.json";

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static LauncherContent Parse(string json) => JsonSerializer.Deserialize<LauncherContent>(json, Options) ?? new();

    /// <summary>
    /// Lit le contenu (URL, ou chemin local pour les essais via CW_LAUNCHER_CONTENT). En cas d'échec, la dernière copie
    /// enregistrée ; sinon un contenu vide. Ne lève jamais d'exception.
    /// </summary>
    public static async Task<LauncherContent> LoadAsync(string url, string cacheFile, HttpClient http, CancellationToken ct)
    {
        try
        {
            var json = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? await http.GetStringAsync(url, ct) : await File.ReadAllTextAsync(url, ct);
            var content = Parse(json);
            Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
            AtomicFile.WriteAllText(cacheFile, json);
            return content;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or JsonException or TaskCanceledException or UnauthorizedAccessException or NotSupportedException)
        {
            Log.Info($"launcher-content.json indisponible ({e.Message}), copie locale utilisée");
            try { return File.Exists(cacheFile) ? Parse(await File.ReadAllTextAsync(cacheFile, ct)) : new(); }
            catch (Exception e2) when (e2 is IOException or JsonException) { return new(); }
        }
    }
}

public sealed class NewsItem
{
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Body { get; set; } = "";
    public string? Image { get; set; }
    public string? Url { get; set; }
}

public sealed class GalleryItem
{
    /// <summary>Adresse https de l'image, ou nom d'une image intégrée (Assets/screenshots).</summary>
    public string Image { get; set; } = "";
    public string Caption { get; set; } = "";
}

public sealed class ContentLinks
{
    public string? Discord { get; set; }
    public string? Website { get; set; }
    public string Github { get; set; } = "https://github.com/Robocnop/create-warfare";
}

public sealed class ContentServer
{
    public string Name { get; set; } = "Serveur Create Warfare";
    public string Host { get; set; } = ServerPing.DefaultHost;
    public int Port { get; set; } = ServerPing.DefaultPort;
    [JsonIgnore] public string Address => Port == 25565 ? Host : $"{Host}:{Port}";
}

// ============================================================ Mémoire (RAM allouée par CurseForge)

/// <summary>RAM du PC, recommandation pour le pack, et RAM allouée par CurseForge à l'instance (minecraftinstance.json).</summary>
public static class MemoryAdvisor
{
    /// <summary>RAM conseillée par le pack (recommendedRam du manifest CurseForge).</summary>
    public const int PackRecommendedMb = 10240;
    public const int MinimumMb = 6144;

    /// <summary>La valeur du pack, sans dépasser la RAM du PC moins 4 Go (pour Windows) ni descendre sous 6 Go. Arrondie au Go.</summary>
    public static int RecommendedMb(long totalMb)
    {
        if (totalMb <= 0) return PackRecommendedMb;
        var rec = Math.Max(MinimumMb, Math.Min(PackRecommendedMb, totalMb - 4096));
        return (int)(rec / 1024 * 1024);
    }

    /// <summary>Plafond du curseur : la RAM du PC moins 2 Go.</summary>
    public static int MaxMb(long totalMb) => totalMb <= 0 ? 16384 : (int)Math.Max(MinimumMb, (totalMb - 2048) / 1024 * 1024);

    public static long TotalPhysicalMb()
    {
        var m = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref m) ? (long)(m.TotalPhys / (1024 * 1024)) : 0;
    }

    /// <summary>RAM allouée à l'instance par CurseForge (allocatedMemory, en Mo), ou null si absente ou illisible.</summary>
    public static int? ReadAllocated(string instanceDir)
    {
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(Path.Combine(instanceDir, "minecraftinstance.json")));
            return node?["allocatedMemory"]?.GetValue<int>();
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or FormatException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Écrit allocatedMemory sans toucher au reste du fichier. À n'appeler que CurseForge fermé :
    /// sinon il réécrit le fichier avec sa propre valeur.
    /// </summary>
    public static void WriteAllocated(string instanceDir, int mb)
    {
        var f = Path.Combine(instanceDir, "minecraftinstance.json");
        var node = JsonNode.Parse(File.ReadAllText(f)) as JsonObject ?? throw new InvalidDataException("minecraftinstance.json illisible");
        node["allocatedMemory"] = mb;
        AtomicFile.WriteAllText(f, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx m);
}
