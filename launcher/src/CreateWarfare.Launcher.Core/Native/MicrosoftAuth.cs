using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CreateWarfare.Launcher.Core.Native;

public enum AuthError
{
    /// <summary>Aucun Client ID Azure : la connexion Microsoft n'est pas encore possible.</summary>
    NotConfigured,
    /// <summary>Réseau coupé, DNS, délai dépassé : réessayer plus tard.</summary>
    Network,
    Cancelled,
    /// <summary>Code de connexion expiré avant d'être saisi.</summary>
    CodeExpired,
    /// <summary>Le joueur a refusé l'autorisation.</summary>
    Declined,
    /// <summary>Jeton de rafraîchissement refusé (expiré, révoqué, mot de passe changé) : nouvelle connexion nécessaire.</summary>
    ReloginRequired,
    /// <summary>Compte Microsoft sans profil Xbox (XErr 2148916233).</summary>
    NoXboxAccount,
    /// <summary>Xbox Live indisponible dans le pays du compte (XErr 2148916235).</summary>
    XboxUnavailable,
    /// <summary>Compte enfant à ajouter à une famille Microsoft (XErr 2148916238).</summary>
    ChildAccount,
    /// <summary>Vérification d'âge demandée (Corée du Sud, XErr 2148916236/7).</summary>
    AdultVerification,
    /// <summary>Application Azure pas encore autorisée par Mojang (403 « Invalid app registration »).</summary>
    AppNotApproved,
    /// <summary>Le compte ne possède pas Minecraft: Java Edition (ou n'a pas encore créé son profil).</summary>
    NoMinecraft,
    RateLimited,
    Unknown,
}

/// <summary>Erreur de connexion. Le message ne contient jamais de jeton.</summary>
public sealed class AuthException(AuthError error, string message, Exception? inner = null) : Exception(message, inner)
{
    public AuthError Error { get; } = error;
}

/// <summary>
/// Client ID de l'application Azure (« Mobile and desktop », comptes Microsoft personnels, flux public / device code).
/// Vide tant que Robo n'a pas enregistré son application et obtenu l'accord de Mojang
/// (https://aka.ms/mce-reviewappid) : jamais le Client ID d'un autre launcher.
/// La variable CW_MS_CLIENT_ID le remplace (essais, ou avant une nouvelle version du launcher).
/// </summary>
public static class AuthConfig
{
    /// <summary>Client ID livré avec le launcher. Vide : la connexion Microsoft est désactivée et Jouer ouvre CurseForge.</summary>
    public const string DefaultClientId = "";
    /// <summary>Variable d'environnement qui remplace <see cref="DefaultClientId"/>.</summary>
    public const string Variable = "CW_MS_CLIENT_ID";

    /// <summary>Client ID effectif (variable d'abord), ou null s'il n'y en a pas.</summary>
    public static string? ClientId =>
        (Environment.GetEnvironmentVariable(Variable) is { } env && env.Trim().Length > 0 ? env.Trim() : DefaultClientId) is { Length: > 0 } id ? id : null;

    /// <summary>Vrai si un Client ID est disponible.</summary>
    public static bool IsConfigured => ClientId != null;
}

public sealed record DeviceCode(string Code, string UserCode, string VerificationUri, TimeSpan ExpiresIn, TimeSpan Interval)
{
    public override string ToString() => $"DeviceCode({UserCode}, {VerificationUri})";
}

public sealed record MsTokens(string AccessToken, string RefreshToken, DateTime ExpiresUtc)
{
    public override string ToString() => "MsTokens(***)";
}

public sealed record MinecraftLogin(string AccessToken, DateTime ExpiresUtc, string Name, string Uuid, string Xuid)
{
    public override string ToString() => $"MinecraftLogin({Name}, {Uuid})";
}

/// <summary>Adresses des services (remplaçables dans les tests).</summary>
public sealed class AuthEndpoints
{
    public string DeviceCode { get; init; } = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    public string Token { get; init; } = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    public string XboxUser { get; init; } = "https://user.auth.xboxlive.com/user/authenticate";
    public string Xsts { get; init; } = "https://xsts.auth.xboxlive.com/xsts/authorize";
    public string MinecraftLogin { get; init; } = "https://api.minecraftservices.com/authentication/login_with_xbox";
    public string MinecraftProfile { get; init; } = "https://api.minecraftservices.com/minecraft/profile";
}

/// <summary>
/// Connexion Microsoft pour Minecraft : code d'appareil (OAuth 2.0 device code) → Xbox Live → XSTS → services Minecraft
/// (jeton du jeu, puis profil : sa présence prouve la possession du jeu).
/// </summary>
public sealed class MicrosoftAuth
{
    public const string Scope = "XboxLive.signin offline_access";

    readonly HttpClient _http;
    readonly string _clientId;
    readonly AuthEndpoints _ep;

    /// <summary>Attente entre deux interrogations du code (remplaçable dans les tests).</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;
    public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    public MicrosoftAuth(HttpClient http, string? clientId, AuthEndpoints? endpoints = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new AuthException(AuthError.NotConfigured, "Connexion Microsoft non configurée : aucun Client ID Azure.");
        _http = http;
        _clientId = clientId;
        _ep = endpoints ?? new AuthEndpoints();
    }

    // ---------------------------------------------------------------- Microsoft (OAuth)

    /// <summary>Demande à Microsoft un code d'appareil (scope <see cref="Scope"/>) à faire saisir au joueur.</summary>
    public async Task<DeviceCode> StartDeviceCodeAsync(CancellationToken ct)
    {
        var (status, json) = await PostFormAsync(_ep.DeviceCode, new() { ["client_id"] = _clientId, ["scope"] = Scope }, ct);
        if (status != HttpStatusCode.OK) throw OAuthError(status, json, "demande du code");
        return new DeviceCode(
            Str(json, "device_code") ?? throw Bad("device_code"),
            Str(json, "user_code") ?? throw Bad("user_code"),
            Str(json, "verification_uri") ?? "https://www.microsoft.com/link",
            TimeSpan.FromSeconds(Int(json, "expires_in") ?? 900),
            TimeSpan.FromSeconds(Math.Max(1, Int(json, "interval") ?? 5)));
    }

    /// <summary>Interroge Microsoft jusqu'à ce que le joueur ait saisi le code (ou refus, expiration, annulation).</summary>
    public async Task<MsTokens> WaitForDeviceCodeAsync(DeviceCode code, CancellationToken ct)
    {
        var deadline = UtcNow() + code.ExpiresIn;
        var interval = code.Interval;
        while (true)
        {
            await DelayAsync(interval, ct);
            if (UtcNow() > deadline) throw new AuthException(AuthError.CodeExpired, "Le code de connexion a expiré.");
            var (status, json) = await PostFormAsync(_ep.Token, new()
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["client_id"] = _clientId,
                ["device_code"] = code.Code,
            }, ct);
            if (status == HttpStatusCode.OK) return Tokens(json);
            switch (Str(json, "error"))
            {
                case "authorization_pending": continue;
                case "slow_down": interval += TimeSpan.FromSeconds(5); continue;
                case "expired_token" or "code_expired": throw new AuthException(AuthError.CodeExpired, "Le code de connexion a expiré.");
                case "authorization_declined" or "access_denied": throw new AuthException(AuthError.Declined, "Connexion refusée sur la page Microsoft.");
                default: throw OAuthError(status, json, "connexion");
            }
        }
    }

    /// <summary>Nouveau jeton d'accès Microsoft à partir du jeton de rafraîchissement (qui change à chaque fois).</summary>
    public async Task<MsTokens> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var (status, json) = await PostFormAsync(_ep.Token, new()
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _clientId,
            ["refresh_token"] = refreshToken,
            ["scope"] = Scope,
        }, ct);
        if (status == HttpStatusCode.OK) return Tokens(json, refreshToken);
        if (Str(json, "error") is "invalid_grant" or "interaction_required" or "invalid_client" || status == HttpStatusCode.Unauthorized)
            throw new AuthException(AuthError.ReloginRequired, "Session Microsoft expirée : reconnecte-toi.");
        throw OAuthError(status, json, "rafraîchissement de la session");
    }

    MsTokens Tokens(JsonNode? json, string? previousRefresh = null) => new(
        Str(json, "access_token") ?? throw Bad("access_token"),
        Str(json, "refresh_token") ?? previousRefresh ?? throw Bad("refresh_token"),
        UtcNow().AddSeconds(Int(json, "expires_in") ?? 3600));

    // ---------------------------------------------------------------- Xbox Live, XSTS, Minecraft

    /// <summary>Jeton Microsoft → jeton Minecraft + profil. Lève NoMinecraft si le compte n'a pas le jeu.</summary>
    public async Task<MinecraftLogin> LoginMinecraftAsync(string msAccessToken, CancellationToken ct)
    {
        // 1. Xbox Live
        var (st, xbl) = await PostJsonAsync(_ep.XboxUser, new JsonObject
        {
            ["Properties"] = new JsonObject
            {
                ["AuthMethod"] = "RPS",
                ["SiteName"] = "user.auth.xboxlive.com",
                ["RpsTicket"] = "d=" + msAccessToken,
            },
            ["RelyingParty"] = "http://auth.xboxlive.com",
            ["TokenType"] = "JWT",
        }, null, ct);
        if (st is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
            throw new AuthException(AuthError.ReloginRequired, $"Xbox Live a refusé la session ({(int)st}) : reconnecte-toi.");
        if (st != HttpStatusCode.OK) throw Http("Xbox Live", st);
        var xblToken = Str(xbl, "Token") ?? throw Bad("Token (Xbox Live)");

        // 2. XSTS pour les services Minecraft
        var (st2, xsts) = await PostJsonAsync(_ep.Xsts, new JsonObject
        {
            ["Properties"] = new JsonObject { ["SandboxId"] = "RETAIL", ["UserTokens"] = new JsonArray(xblToken) },
            ["RelyingParty"] = "rp://api.minecraftservices.com/",
            ["TokenType"] = "JWT",
        }, null, ct);
        if (st2 == HttpStatusCode.Unauthorized) throw XstsError(xsts);
        if (st2 != HttpStatusCode.OK) throw Http("XSTS", st2);
        var xstsToken = Str(xsts, "Token") ?? throw Bad("Token (XSTS)");
        var claims = xsts?["DisplayClaims"]?["xui"] is JsonArray { Count: > 0 } xui ? xui[0] : null;
        var uhs = Str(claims, "uhs") ?? throw Bad("uhs");
        var xuid = Str(claims, "xid") ?? "0";

        // 3. Jeton Minecraft
        var (st3, mc) = await PostJsonAsync(_ep.MinecraftLogin, new JsonObject { ["identityToken"] = $"XBL3.0 x={uhs};{xstsToken}" }, null, ct);
        if (st3 == HttpStatusCode.Forbidden)
            throw new AuthException(AuthError.AppNotApproved,
                "Les services Minecraft refusent ce launcher : son application Azure n'est pas (encore) autorisée par Mojang.");
        if (st3 == (HttpStatusCode)429) throw new AuthException(AuthError.RateLimited, "Trop de connexions : réessaie dans quelques minutes.");
        if (st3 != HttpStatusCode.OK) throw Http("Minecraft (connexion)", st3);
        var mcToken = Str(mc, "access_token") ?? throw Bad("access_token (Minecraft)");
        var expires = UtcNow().AddSeconds(Int(mc, "expires_in") ?? 86400);

        // 4. Profil : absent (404) si le compte n'a pas acheté Minecraft: Java Edition
        var (st4, profile) = await GetJsonAsync(_ep.MinecraftProfile, mcToken, ct);
        if (st4 == HttpStatusCode.NotFound)
            throw new AuthException(AuthError.NoMinecraft, "Ce compte Microsoft ne possède pas Minecraft: Java Edition (ou n'a pas encore choisi de pseudo sur minecraft.net).");
        if (st4 == HttpStatusCode.Unauthorized) throw new AuthException(AuthError.ReloginRequired, "Jeton Minecraft refusé : reconnecte-toi.");
        if (st4 == (HttpStatusCode)429) throw new AuthException(AuthError.RateLimited, "Trop de connexions : réessaie dans quelques minutes.");
        if (st4 != HttpStatusCode.OK) throw Http("Minecraft (profil)", st4);
        return new MinecraftLogin(mcToken, expires, Str(profile, "name") ?? throw Bad("name"), Str(profile, "id") ?? throw Bad("id"), xuid);
    }

    static AuthException XstsError(JsonNode? json)
    {
        var code = json?["XErr"] is JsonValue v && v.TryGetValue<long>(out var n) ? n : 0;
        return code switch
        {
            2148916233 => new AuthException(AuthError.NoXboxAccount, "Ce compte Microsoft n'a pas de profil Xbox : connecte-toi une fois sur minecraft.net ou xbox.com pour le créer."),
            2148916235 => new AuthException(AuthError.XboxUnavailable, "Xbox Live n'est pas disponible dans le pays de ce compte."),
            2148916236 or 2148916237 => new AuthException(AuthError.AdultVerification, "Ce compte doit d'abord confirmer l'âge du joueur sur la page Xbox."),
            2148916238 => new AuthException(AuthError.ChildAccount, "Compte enfant : un adulte doit l'ajouter à une famille Microsoft pour autoriser Minecraft."),
            _ => new AuthException(AuthError.Unknown, $"Xbox Live a refusé la connexion (XErr {code})."),
        };
    }

    // ---------------------------------------------------------------- HTTP

    async Task DelayAsync(TimeSpan t, CancellationToken ct)
    {
        try { await Delay(t, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw new AuthException(AuthError.Cancelled, "Connexion annulée."); }
    }

    Task<(HttpStatusCode, JsonNode?)> PostFormAsync(string url, Dictionary<string, string> form, CancellationToken ct) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) }, ct);

    Task<(HttpStatusCode, JsonNode?)> PostJsonAsync(string url, JsonObject body, string? bearer, CancellationToken ct) =>
        SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
            if (bearer != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return req;
        }, ct);

    Task<(HttpStatusCode, JsonNode?)> GetJsonAsync(string url, string bearer, CancellationToken ct) =>
        SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return req;
        }, ct);

    async Task<(HttpStatusCode, JsonNode?)> SendAsync(Func<HttpRequestMessage> make, CancellationToken ct)
    {
        using var req = make();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            using var res = await _http.SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            JsonNode? json = null;
            if (text.Length > 0)
                try { json = JsonNode.Parse(text); } catch (JsonException) { }
            return (res.StatusCode, json);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new AuthException(AuthError.Cancelled, "Connexion annulée.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            // pas de réponse : réseau coupé, DNS, délai dépassé. L'URL suffit, jamais le contenu de la requête.
            throw new AuthException(AuthError.Network, $"Serveur injoignable ({req.RequestUri?.Host}) : vérifie ta connexion.", e);
        }
    }

    static AuthException OAuthError(HttpStatusCode status, JsonNode? json, string step)
    {
        var err = Str(json, "error");
        if (status == (HttpStatusCode)429) return new AuthException(AuthError.RateLimited, "Trop de connexions : réessaie dans quelques minutes.");
        if (err is "invalid_client" or "unauthorized_client")
            return new AuthException(AuthError.NotConfigured, $"Client ID Azure refusé par Microsoft ({err}) : vérifie l'application Azure (comptes personnels, flux public).");
        return new AuthException(AuthError.Unknown, $"Microsoft a refusé la {step} ({(int)status}{(err == null ? "" : ", " + err)}).");
    }

    static AuthException Http(string service, HttpStatusCode st) =>
        (int)st >= 500
            ? new AuthException(AuthError.Network, $"{service} est indisponible pour le moment ({(int)st}). Réessaie plus tard.")
            : new AuthException(AuthError.Unknown, $"{service} a répondu {(int)st}.");

    static AuthException Bad(string field) => new(AuthError.Unknown, $"Réponse inattendue du service de connexion (champ {field} absent).");

    static string? Str(JsonNode? n, string key) =>
        n is JsonObject o && o.TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : null;

    static int? Int(JsonNode? n, string key) =>
        n is JsonObject o && o.TryGetPropertyValue(key, out var v) && v is JsonValue jv
            ? jv.TryGetValue<int>(out var i) ? i : jv.TryGetValue<long>(out var l) ? (int)l : jv.TryGetValue<string>(out var s) && int.TryParse(s, out var p) ? p : null
            : null;
}
