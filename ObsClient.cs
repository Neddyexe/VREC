using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed class ObsClient : IAsyncDisposable
{
    private readonly Uri uri;
    private readonly string password;
    private ClientWebSocket? socket;
    private int nextId;
    public ObsClient(string url, string password) { uri = new Uri(url); this.password = password; }

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (socket?.State == WebSocketState.Open) return;
        if (socket is not null) socket.Dispose();
        socket = new ClientWebSocket();
        await socket.ConnectAsync(uri, ct);
        using var hello = JsonDocument.Parse(await ReceiveAsync(ct));
        if (hello.RootElement.GetProperty("op").GetInt32() != 0) throw new Exception("Expected OBS hello message.");
        var data = hello.RootElement.GetProperty("d");
        string? authentication = null;
        if (data.TryGetProperty("authentication", out var auth))
        {
            if (string.IsNullOrEmpty(password)) throw new Exception("OBS requires a WebSocket password.");
            string salt = auth.GetProperty("salt").GetString()!;
            string challenge = auth.GetProperty("challenge").GetString()!;
            string secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
            authentication = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
        }
        string identify = authentication is null
            ? JsonSerializer.Serialize(new { op = 1, d = new { rpcVersion = 1 } })
            : JsonSerializer.Serialize(new { op = 1, d = new { rpcVersion = 1, authentication } });
        await SendAsync(identify, ct);
        using var identified = JsonDocument.Parse(await ReceiveAsync(ct));
        if (identified.RootElement.GetProperty("op").GetInt32() != 2)
            throw new Exception("OBS login failed. Check the WebSocket password and port 4455.");
    }

    public async Task<bool> IsRecordingAsync(CancellationToken ct)
    {
        using var result = await RequestAsync("GetRecordStatus", ct);
        return result.RootElement.GetProperty("d").GetProperty("responseData").GetProperty("outputActive").GetBoolean();
    }

    public async Task<JsonDocument> RequestAsync(string type, CancellationToken ct)
    {
        await ConnectAsync(ct);
        var requestId = Interlocked.Increment(ref nextId).ToString();
        await SendAsync(JsonSerializer.Serialize(new { op = 6, d = new { requestType = type, requestId } }), ct);
        while (true)
        {
            using var message = JsonDocument.Parse(await ReceiveAsync(ct));
            if (message.RootElement.GetProperty("op").GetInt32() != 7) continue;
            var d = message.RootElement.GetProperty("d");
            if (d.GetProperty("requestId").GetString() != requestId) continue;
            var status = d.GetProperty("requestStatus");
            if (!status.GetProperty("result").GetBoolean())
                throw new Exception($"OBS {type} failed: {status.GetProperty("comment").GetString()}");
            return JsonDocument.Parse(message.RootElement.GetRawText());
        }
    }

    private async Task SendAsync(string content, CancellationToken ct)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        await socket!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    private async Task<string> ReceiveAsync(CancellationToken ct)
    {
        using var ms = new MemoryStream();
        byte[] buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await socket!.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close) throw new Exception("OBS closed the WebSocket connection.");
            if (ms.Length + result.Count > 1024 * 1024) throw new InvalidDataException("OBS message too large.");
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public ValueTask DisposeAsync() { socket?.Dispose(); return ValueTask.CompletedTask; }
}
