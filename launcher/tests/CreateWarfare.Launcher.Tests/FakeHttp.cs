using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CreateWarfare.Launcher.Tests;

/// <summary>
/// Petit serveur HTTP local pour simuler un CDN capricieux : coupure au milieu, connexion muette, erreur 500, reprise (Range).
/// </summary>
public sealed class FakeHttp : IDisposable
{
    public sealed record Request(string Path, long RangeStart, int Number);
    /// <param name="CutAfter">Ferme la connexion après ce nombre d'octets du corps (-1 : tout envoyer).</param>
    /// <param name="Stall">Envoie les en-têtes puis plus rien, jusqu'à la fermeture du serveur.</param>
    public sealed record Response(int Status, byte[] Body, int CutAfter = -1, bool Stall = false);

    readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    readonly CancellationTokenSource _stop = new();
    public Func<Request, Response> Handler { get; set; } = _ => new Response(404, Array.Empty<byte>());
    public List<Request> Requests { get; } = new();
    public string Url { get; }

    public FakeHttp()
    {
        _listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
        _ = Task.Run(AcceptLoopAsync);
    }

    async Task AcceptLoopAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                _ = Task.Run(() => ServeAsync(client));
            }
        }
        catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }

    async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var s = client.GetStream();
                var head = new StringBuilder();
                var one = new byte[1];
                while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                {
                    if (await s.ReadAsync(one, _stop.Token) == 0) return;
                    head.Append((char)one[0]);
                }
                var lines = head.ToString().Split("\r\n");
                var path = lines[0].Split(' ')[1];
                long start = 0;
                foreach (var l in lines)
                    if (l.StartsWith("Range: bytes=", StringComparison.OrdinalIgnoreCase))
                        start = long.Parse(l["Range: bytes=".Length..].TrimEnd('-'));
                Request req;
                lock (Requests)
                {
                    req = new Request(path, start, Requests.Count + 1);
                    Requests.Add(req);
                }
                var res = Handler(req);
                var body = res.Body;
                var status = res.Status;
                var extra = "";
                if (status == 200 && start > 0 && start < body.Length)
                {
                    extra = $"Content-Range: bytes {start}-{body.Length - 1}/{body.Length}\r\n";
                    body = body[(int)start..];
                    status = 206;
                }
                var header = $"HTTP/1.1 {status} X\r\nContent-Length: {body.Length}\r\n{extra}Connection: close\r\n\r\n";
                await s.WriteAsync(Encoding.ASCII.GetBytes(header), _stop.Token);
                await s.FlushAsync(_stop.Token);
                if (res.Stall)
                {
                    await Task.Delay(Timeout.Infinite, _stop.Token);
                    return;
                }
                var n = res.CutAfter >= 0 ? Math.Min(res.CutAfter, body.Length) : body.Length;
                await s.WriteAsync(body.AsMemory(0, n), _stop.Token);
                await s.FlushAsync(_stop.Token);
                if (n < body.Length)
                {
                    await Task.Delay(200, _stop.Token); // laisser le client lire ce qui est parti
                    client.Client.Close(0);             // puis coupure franche
                }
            }
            catch (Exception e) when (e is OperationCanceledException or IOException or SocketException or ObjectDisposedException) { }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }
}
