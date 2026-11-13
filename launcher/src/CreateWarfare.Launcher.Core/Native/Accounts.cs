using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Native;

/// <summary>Identité passée au jeu (--username, --uuid, --accessToken, --userType, --xuid). ToString ne montre jamais le jeton.</summary>
public sealed record GameSession(string Name, string Uuid, string AccessToken, string UserType, string Xuid, bool Offline)
{
    public override string ToString() => $"{Name} ({(Offline ? "hors ligne" : UserType)}, {Uuid})";
}

/// <summary>
/// Compte Microsoft mémorisé (LauncherSettings.Account) : jetons chiffrés par DPAPI, jeton Minecraft gardé jusqu'à son
/// expiration (24 h) pour ne pas refaire toute la chaîne de connexion à chaque partie.
/// </summary>
public static class AccountStore
{
    /// <summary>Un jeton Minecraft qui expire dans moins de 5 min n'est plus utilisé (une partie peut démarrer lentement).</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(5);

    /// <summary>Session tirée du jeton Minecraft encore valide, sans réseau ; null s'il faut le renouveler.</summary>
    public static GameSession? Cached(MsAccount? a, DateTime nowUtc)
    {
        if (a == null || a.McTokenExpiresUtc - Margin <= nowUtc) return null;
        var token = TokenVault.Unprotect(a.McToken);
        return token == null ? null : new GameSession(a.Name, a.Uuid, token, "msa", a.Xuid ?? "0", Offline: false);
    }

    public static MsAccount ToAccount(MsTokens ms, MinecraftLogin mc, DateTime nowUtc) => new()
    {
        Name = mc.Name,
        Uuid = mc.Uuid,
        Xuid = mc.Xuid,
        RefreshToken = TokenVault.Protect(ms.RefreshToken),
        McToken = TokenVault.Protect(mc.AccessToken),
        McTokenExpiresUtc = mc.ExpiresUtc,
        LastLoginUtc = nowUtc,
    };

    static GameSession Session(MinecraftLogin mc) => new(mc.Name, mc.Uuid, mc.AccessToken, "msa", mc.Xuid, Offline: false);

    /// <summary>Renouvelle la session avec le jeton de rafraîchissement (pas d'action du joueur). ReloginRequired s'il est refusé.</summary>
    public static async Task<(GameSession Session, MsAccount Account)> RefreshAsync(MsAccount a, MicrosoftAuth auth, CancellationToken ct)
    {
        var refresh = TokenVault.Unprotect(a.RefreshToken)
            ?? throw new AuthException(AuthError.ReloginRequired, "Session enregistrée illisible (autre compte Windows ?) : reconnecte-toi.");
        var ms = await auth.RefreshAsync(refresh, ct);
        var mc = await auth.LoginMinecraftAsync(ms.AccessToken, ct);
        return (Session(mc), ToAccount(ms, mc, auth.UtcNow()));
    }

    /// <summary>Connexion complète par code d'appareil : <paramref name="show"/> affiche le code et le lien au joueur.</summary>
    public static async Task<(GameSession Session, MsAccount Account)> LoginAsync(MicrosoftAuth auth, Action<DeviceCode> show, CancellationToken ct)
    {
        var code = await auth.StartDeviceCodeAsync(ct);
        show(code);
        var ms = await auth.WaitForDeviceCodeAsync(code, ct);
        var mc = await auth.LoginMinecraftAsync(ms.AccessToken, ct);
        return (Session(mc), ToAccount(ms, mc, auth.UtcNow()));
    }
}

/// <summary>
/// Mode « hors ligne de test » : CW_OFFLINE_TEST=pseudo lance le jeu sans compte Microsoft (jeton « 0 »), pour essayer
/// le lancement natif. Jamais proposé dans l'interface ; un serveur en mode online (comme celui du pack) refuse ce joueur.
/// </summary>
public static partial class OfflineTest
{
    public const string Variable = "CW_OFFLINE_TEST";

    public static GameSession? FromEnvironment() => From(Environment.GetEnvironmentVariable(Variable));

    /// <summary>Pseudo Minecraft valide (3 à 16 lettres, chiffres ou _) ; null sinon.</summary>
    public static GameSession? From(string? name)
    {
        name = name?.Trim();
        if (name == null || !NameRx().IsMatch(name)) return null;
        return new GameSession(name, OfflineUuid(name), "0", "legacy", "0", Offline: true);
    }

    /// <summary>Même UUID que le jeu pour un joueur hors ligne : UUID v3 (MD5) de « OfflinePlayer:&lt;pseudo&gt; », sans tirets.</summary>
    public static string OfflineUuid(string name)
    {
        var b = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
        b[6] = (byte)((b[6] & 0x0f) | 0x30);
        b[8] = (byte)((b[8] & 0x3f) | 0x80);
        return Convert.ToHexString(b).ToLowerInvariant();
    }

    [GeneratedRegex("^[A-Za-z0-9_]{3,16}$")]
    private static partial Regex NameRx();
}
