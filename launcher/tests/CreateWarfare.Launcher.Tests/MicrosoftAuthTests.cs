using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using CreateWarfare.Launcher.Core.Native;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

/// <summary>HttpClient simulé : chaque requête passe par <see cref="Handler"/> (adresse, corps) ; toutes sont gardées.</summary>
sealed class StubHttp : HttpMessageHandler
{
    public Func<HttpRequestMessage, string, HttpResponseMessage> Handler { get; set; } = (_, _) => new HttpResponseMessage(HttpStatusCode.NotFound);
    public List<(string Url, string Body, string? Auth)> Calls { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
        lock (Calls) Calls.Add((request.RequestUri!.ToString(), body, request.Headers.Authorization?.ToString()));
        return Handler(request, body);
    }

    public static HttpResponseMessage Json(HttpStatusCode code, string json) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

public class MicrosoftAuthTests
{
    const string ClientId = "00000000-test-client-id";

    /// <summary>Services simulés : Microsoft (code, jeton), Xbox Live, XSTS, Minecraft. <paramref name="polls"/> réponses avant le jeton.</summary>
    static StubHttp Services(Queue<string>? polls = null, HttpStatusCode xsts = HttpStatusCode.OK, string xstsBody = "",
        HttpStatusCode mcLogin = HttpStatusCode.OK, HttpStatusCode profile = HttpStatusCode.OK, string? refreshError = null)
    {
        var ep = new AuthEndpoints();
        return new StubHttp
        {
            Handler = (req, body) =>
            {
                var url = req.RequestUri!.ToString();
                if (url == ep.DeviceCode)
                    return StubHttp.Json(HttpStatusCode.OK, """{"device_code":"DEV-123","user_code":"ABCD-EFGH","verification_uri":"https://www.microsoft.com/link","expires_in":900,"interval":5}""");
                if (url == ep.Token)
                {
                    if (body.Contains("grant_type=refresh_token"))
                        return refreshError != null
                            ? StubHttp.Json(HttpStatusCode.BadRequest, $$"""{"error":"{{refreshError}}"}""")
                            : StubHttp.Json(HttpStatusCode.OK, """{"access_token":"MS-ACCESS-2","refresh_token":"MS-REFRESH-2","expires_in":3600}""");
                    if (polls != null && polls.Count > 0)
                        return StubHttp.Json(HttpStatusCode.BadRequest, $$"""{"error":"{{polls.Dequeue()}}"}""");
                    return StubHttp.Json(HttpStatusCode.OK, """{"access_token":"MS-ACCESS","refresh_token":"MS-REFRESH","expires_in":3600}""");
                }
                if (url == ep.XboxUser)
                    return StubHttp.Json(HttpStatusCode.OK, """{"Token":"XBL-TOKEN","DisplayClaims":{"xui":[{"uhs":"UHS1"}]}}""");
                if (url == ep.Xsts)
                    return xsts == HttpStatusCode.OK
                        ? StubHttp.Json(HttpStatusCode.OK, """{"Token":"XSTS-TOKEN","DisplayClaims":{"xui":[{"uhs":"UHS1"}]}}""")
                        : StubHttp.Json(xsts, xstsBody);
                if (url == ep.MinecraftLogin)
                    return mcLogin == HttpStatusCode.OK
                        ? StubHttp.Json(HttpStatusCode.OK, """{"username":"x","access_token":"MC-TOKEN","expires_in":86400,"token_type":"Bearer"}""")
                        : StubHttp.Json(mcLogin, """{"path":"/authentication/login_with_xbox","errorMessage":"Invalid app registration, see https://aka.ms/AppRegInfo for more information"}""");
                if (url == ep.MinecraftProfile)
                    return profile == HttpStatusCode.OK
                        ? StubHttp.Json(HttpStatusCode.OK, """{"id":"a4be82ed4fb847f7895e08b016d04423","name":"Robocnop","skins":[]}""")
                        : StubHttp.Json(profile, """{"path":"/minecraft/profile","errorType":"NOT_FOUND","error":"NOT_FOUND"}""");
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            },
        };
    }

    static MicrosoftAuth Auth(StubHttp stub, List<TimeSpan>? delays = null) =>
        new(new HttpClient(stub), ClientId) { Delay = (t, _) => { delays?.Add(t); return Task.CompletedTask; } };

    [Fact]
    public async Task Connexion_complete_par_code()
    {
        var stub = Services(new Queue<string>(new[] { "authorization_pending", "slow_down", "authorization_pending" }));
        var delays = new List<TimeSpan>();
        DeviceCode? shown = null;
        var (session, account) = await AccountStore.LoginAsync(Auth(stub, delays), c => shown = c, default);

        Assert.Equal("ABCD-EFGH", shown!.UserCode);
        Assert.Equal("https://www.microsoft.com/link", shown.VerificationUri);
        Assert.Equal("Robocnop", session.Name);
        Assert.Equal("a4be82ed4fb847f7895e08b016d04423", session.Uuid);
        Assert.Equal("MC-TOKEN", session.AccessToken);
        Assert.Equal("msa", session.UserType);
        Assert.False(session.Offline);
        // slow_down : 5 s de plus entre deux interrogations
        Assert.Equal(new[] { 5, 5, 10, 10 }, delays.Select(d => (int)d.TotalSeconds));

        // compte enregistré : jetons chiffrés, jamais en clair
        Assert.Equal("Robocnop", account.Name);
        Assert.Equal("MS-REFRESH", TokenVault.Unprotect(account.RefreshToken));
        Assert.Equal("MC-TOKEN", TokenVault.Unprotect(account.McToken));
        Assert.DoesNotContain("MS-REFRESH", account.RefreshToken);

        // requêtes : client_id + portée, ticket RPS « d= », jeton d'identité XBL3.0, profil avec le jeton Minecraft
        var dc = stub.Calls.First(c => c.Url.EndsWith("/devicecode"));
        Assert.Contains("client_id=" + ClientId, dc.Body);
        Assert.Contains("scope=XboxLive.signin+offline_access", dc.Body);
        Assert.Equal("d=MS-ACCESS", JsonNode.Parse(stub.Calls.First(c => c.Url.Contains("user.auth.xboxlive.com")).Body)!["Properties"]!["RpsTicket"]!.GetValue<string>());
        var xsts = JsonNode.Parse(stub.Calls.First(c => c.Url.Contains("xsts")).Body)!;
        Assert.Equal("rp://api.minecraftservices.com/", xsts["RelyingParty"]!.GetValue<string>());
        Assert.Equal("XBL-TOKEN", xsts["Properties"]!["UserTokens"]![0]!.GetValue<string>());
        Assert.Equal("XBL3.0 x=UHS1;XSTS-TOKEN", JsonNode.Parse(stub.Calls.First(c => c.Url.EndsWith("login_with_xbox")).Body)!["identityToken"]!.GetValue<string>());
        Assert.Equal("Bearer MC-TOKEN", stub.Calls.First(c => c.Url.EndsWith("/minecraft/profile")).Auth);
        Assert.Equal(4, stub.Calls.Count(c => c.Url.EndsWith("/token")));
    }

    [Fact]
    public async Task Rafraichissement_silencieux()
    {
        var stub = Services();
        var acc = AccountStore.ToAccount(new MsTokens("old", "MS-REFRESH", DateTime.UtcNow), new MinecraftLogin("old-mc", DateTime.UtcNow.AddMinutes(-1), "Robocnop", "id", "0"), DateTime.UtcNow);
        Assert.Null(AccountStore.Cached(acc, DateTime.UtcNow)); // jeton Minecraft expiré
        var (session, updated) = await AccountStore.RefreshAsync(acc, Auth(stub), default);
        Assert.Equal("MC-TOKEN", session.AccessToken);
        Assert.Equal("MS-REFRESH-2", TokenVault.Unprotect(updated.RefreshToken)); // le jeton tourne
        var refresh = stub.Calls.Single(c => c.Url.EndsWith("/token"));
        Assert.Contains("refresh_token=MS-REFRESH", refresh.Body);
        Assert.DoesNotContain(stub.Calls, c => c.Url.EndsWith("/devicecode"));
        Assert.NotNull(AccountStore.Cached(updated, DateTime.UtcNow));
    }

    [Fact]
    public async Task Jeton_de_rafraichissement_expire_ou_revoque()
    {
        var acc = AccountStore.ToAccount(new MsTokens("a", "MS-REFRESH", DateTime.UtcNow), new MinecraftLogin("m", DateTime.UtcNow, "R", "id", "0"), DateTime.UtcNow);
        var e = await Assert.ThrowsAsync<AuthException>(() => AccountStore.RefreshAsync(acc, Auth(Services(refreshError: "invalid_grant")), default));
        Assert.Equal(AuthError.ReloginRequired, e.Error);
        acc.RefreshToken = "illisible";
        e = await Assert.ThrowsAsync<AuthException>(() => AccountStore.RefreshAsync(acc, Auth(Services()), default));
        Assert.Equal(AuthError.ReloginRequired, e.Error);
    }

    [Fact]
    public async Task Compte_sans_Minecraft()
    {
        var e = await Assert.ThrowsAsync<AuthException>(() => Auth(Services(profile: HttpStatusCode.NotFound)).LoginMinecraftAsync("MS-ACCESS", default));
        Assert.Equal(AuthError.NoMinecraft, e.Error);
    }

    [Theory]
    [InlineData(2148916233L, AuthError.NoXboxAccount)]
    [InlineData(2148916235L, AuthError.XboxUnavailable)]
    [InlineData(2148916236L, AuthError.AdultVerification)]
    [InlineData(2148916238L, AuthError.ChildAccount)]
    [InlineData(1L, AuthError.Unknown)]
    public async Task Refus_XSTS(long xerr, AuthError expected)
    {
        var stub = Services(xsts: HttpStatusCode.Unauthorized, xstsBody: $$"""{"Identity":"0","XErr":{{xerr}},"Message":"","Redirect":"https://start.ui.xboxlive.com/AddChildToFamily"}""");
        var e = await Assert.ThrowsAsync<AuthException>(() => Auth(stub).LoginMinecraftAsync("MS-ACCESS", default));
        Assert.Equal(expected, e.Error);
    }

    [Fact]
    public async Task Application_Azure_pas_encore_autorisee_par_Mojang()
    {
        var e = await Assert.ThrowsAsync<AuthException>(() => Auth(Services(mcLogin: HttpStatusCode.Forbidden)).LoginMinecraftAsync("MS-ACCESS", default));
        Assert.Equal(AuthError.AppNotApproved, e.Error);
    }

    [Theory]
    [InlineData("expired_token", AuthError.CodeExpired)]
    [InlineData("authorization_declined", AuthError.Declined)]
    [InlineData("invalid_client", AuthError.NotConfigured)]
    public async Task Code_refuse(string error, AuthError expected)
    {
        var stub = Services(new Queue<string>(new[] { "authorization_pending", error }));
        var auth = Auth(stub);
        var code = await auth.StartDeviceCodeAsync(default);
        var e = await Assert.ThrowsAsync<AuthException>(() => auth.WaitForDeviceCodeAsync(code, default));
        Assert.Equal(expected, e.Error);
    }

    [Fact]
    public async Task Code_expire_cote_launcher()
    {
        var stub = Services(new Queue<string>(Enumerable.Repeat("authorization_pending", 1000)));
        var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        var auth = new MicrosoftAuth(new HttpClient(stub), ClientId) { UtcNow = () => now, Delay = (t, _) => { now += t; return Task.CompletedTask; } };
        var code = await auth.StartDeviceCodeAsync(default);
        var e = await Assert.ThrowsAsync<AuthException>(() => auth.WaitForDeviceCodeAsync(code, default));
        Assert.Equal(AuthError.CodeExpired, e.Error);
        Assert.InRange(stub.Calls.Count(c => c.Url.EndsWith("/token")), 170, 181); // 900 s / 5 s
    }

    [Fact]
    public async Task Annulation_par_le_joueur()
    {
        using var cts = new CancellationTokenSource();
        var stub = Services(new Queue<string>(Enumerable.Repeat("authorization_pending", 1000)));
        var auth = new MicrosoftAuth(new HttpClient(stub), ClientId) { Delay = (t, ct) => { cts.Cancel(); return Task.Delay(t, ct); } };
        var code = await auth.StartDeviceCodeAsync(default);
        var e = await Assert.ThrowsAsync<AuthException>(() => auth.WaitForDeviceCodeAsync(code, cts.Token));
        Assert.Equal(AuthError.Cancelled, e.Error);
    }

    [Fact]
    public async Task Reseau_coupe()
    {
        var stub = new StubHttp { Handler = (_, _) => throw new HttpRequestException("No such host is known.") };
        var e = await Assert.ThrowsAsync<AuthException>(() => Auth(stub).StartDeviceCodeAsync(default));
        Assert.Equal(AuthError.Network, e.Error);
        Assert.Contains("login.microsoftonline.com", e.Message);

        var acc = AccountStore.ToAccount(new MsTokens("a", "MS-REFRESH", DateTime.UtcNow), new MinecraftLogin("m", DateTime.UtcNow, "R", "id", "0"), DateTime.UtcNow);
        e = await Assert.ThrowsAsync<AuthException>(() => AccountStore.RefreshAsync(acc, Auth(stub), default));
        Assert.Equal(AuthError.Network, e.Error);

        // 5xx : service indisponible, pas une erreur du compte
        var down = Services();
        var inner = down.Handler;
        down.Handler = (r, b) => r.RequestUri!.Host == "user.auth.xboxlive.com" ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : inner(r, b);
        e = await Assert.ThrowsAsync<AuthException>(() => Auth(down).LoginMinecraftAsync("MS-ACCESS", default));
        Assert.Equal(AuthError.Network, e.Error);
    }

    [Fact]
    public void Sans_client_id_pas_de_connexion()
    {
        var e = Assert.Throws<AuthException>(() => new MicrosoftAuth(new HttpClient(new StubHttp()), null));
        Assert.Equal(AuthError.NotConfigured, e.Error);
        Assert.Throws<AuthException>(() => new MicrosoftAuth(new HttpClient(new StubHttp()), "  "));
        // aucun Client ID d'un autre launcher n'est embarqué : vide tant que Robo n'a pas son application Azure
        Assert.Equal("", AuthConfig.DefaultClientId);
    }

    [Fact]
    public async Task Aucun_jeton_dans_les_messages()
    {
        var secrets = new[] { "MS-ACCESS", "MS-REFRESH", "XBL-TOKEN", "XSTS-TOKEN", "MC-TOKEN" };
        var cases = new Func<Task>[]
        {
            () => Auth(Services(profile: HttpStatusCode.NotFound)).LoginMinecraftAsync("MS-ACCESS", default),
            () => Auth(Services(mcLogin: HttpStatusCode.Forbidden)).LoginMinecraftAsync("MS-ACCESS", default),
            () => Auth(Services(xsts: HttpStatusCode.Unauthorized, xstsBody: """{"XErr":2148916233}""")).LoginMinecraftAsync("MS-ACCESS", default),
            () => Auth(Services(refreshError: "invalid_grant")).RefreshAsync("MS-REFRESH", default),
            () => Auth(Services(profile: HttpStatusCode.BadGateway)).LoginMinecraftAsync("MS-ACCESS", default),
        };
        foreach (var c in cases)
        {
            var e = await Assert.ThrowsAsync<AuthException>(c);
            foreach (var s in secrets) Assert.DoesNotContain(s, e.ToString());
        }
        var login = await Auth(Services()).LoginMinecraftAsync("MS-ACCESS", default);
        Assert.DoesNotContain("MC-TOKEN", login.ToString());
        Assert.DoesNotContain("MS-REFRESH", new MsTokens("MS-ACCESS", "MS-REFRESH", DateTime.UtcNow).ToString());
        Assert.DoesNotContain("MC-TOKEN", new GameSession("R", "u", "MC-TOKEN", "msa", "0", false).ToString());
    }
}

/// <summary>Détection du jeu en cours (Sodium réécrit la ligne de commande : on regarde aussi qui tient latest.log).</summary>
public class ProcessGuardTests
{
    [Fact]
    public void Fichier_ouvert_vu_par_le_gestionnaire_de_redemarrage()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cw-guard-" + Guid.NewGuid().ToString("N")[..6]);
        var log = Path.Combine(dir, "logs", "latest.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        File.WriteAllText(log, "x");
        try
        {
            Assert.DoesNotContain(Environment.ProcessId, ProcessGuard.FileUsers(log));
            using (new FileStream(log, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                Assert.Contains(Environment.ProcessId, ProcessGuard.FileUsers(log));
                // tenu par testhost, pas par java : ce n'est pas le jeu
                Assert.False(ProcessGuard.LogInUse(dir));
            }
            Assert.False(ProcessGuard.LogInUse(Path.Combine(dir, "absent")));
            Assert.False(ProcessGuard.GameRunning(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Marqueur_du_jeu_lance_par_le_launcher()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cw-marker-" + Guid.NewGuid().ToString("N")[..6]);
        using var self = Process.GetCurrentProcess();
        GameMarker.Write(dir, self);
        try
        {
            Assert.True(GameMarker.Alive(dir));
            Assert.False(GameMarker.Alive(dir + "-autre"));
            GameMarker.Clear(dir, self.Id + 1); // autre PID : rien n'est effacé
            Assert.True(GameMarker.Alive(dir));
        }
        finally { GameMarker.Clear(dir, self.Id); }
        Assert.False(GameMarker.Alive(dir));
    }
}
