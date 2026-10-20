using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class ObsProtocolTest
{
    public static async Task RunAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)); var ct = timeout.Token;
        var server = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(ct);
            await using var stream = connection.GetStream();
            using var header = new MemoryStream(); byte[] one = new byte[1];
            while (header.Length < 8192) {
                if (await stream.ReadAsync(one, ct) == 0) throw new IOException("Missing WebSocket handshake");
                header.WriteByte(one[0]);
                if (header.Length >= 4 && Encoding.ASCII.GetString(header.GetBuffer(), (int)header.Length - 4, 4) == "\r\n\r\n") break;
            }
            string key = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n")
                .Single(x => x.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)).Split(':', 2)[1].Trim();
            string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n"), ct);
            using var socket = WebSocket.CreateFromStream(stream, true, null, Timeout.InfiniteTimeSpan);
            await Send(socket, """{"op":0,"d":{"authentication":{"salt":"salt","challenge":"challenge"}}}""", ct);
            using var identify = JsonDocument.Parse(await Receive(socket, ct));
            string secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("test-password" + "salt")));
            string expected = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + "challenge")));
            if (identify.RootElement.GetProperty("d").GetProperty("authentication").GetString() != expected) throw new Exception("Incorrect OBS authentication");
            await Send(socket, """{"op":2,"d":{"negotiatedRpcVersion":1}}""", ct);
            bool active = false;
            foreach (var expectedType in new[] { "GetRecordStatus", "StartRecord", "GetRecordStatus", "StopRecord", "GetRecordStatus" }) {
                using var request = JsonDocument.Parse(await Receive(socket, ct)); var d = request.RootElement.GetProperty("d");
                string type = d.GetProperty("requestType").GetString()!;
                if (type != expectedType) throw new Exception("Wrong request order");
                if (type == "StartRecord") active = true; if (type == "StopRecord") active = false;
                await Send(socket, """{"op":5,"d":{"eventType":"RecordStateChanged"}}""", ct);
                string response = JsonSerializer.Serialize(new { op = 7, d = new { requestId = d.GetProperty("requestId").GetString(), requestStatus = new { result = true, code = 100 }, responseData = new { outputActive = active } } });
                byte[] bytes = Encoding.UTF8.GetBytes(response);
                await socket.SendAsync(new ArraySegment<byte>(bytes, 0, 10), WebSocketMessageType.Text, false, ct);
                await socket.SendAsync(new ArraySegment<byte>(bytes, 10, bytes.Length - 10), WebSocketMessageType.Text, true, ct);
            }
        }, ct);
        await using var client = new ObsClient($"ws://127.0.0.1:{port}/", "test-password");
        try {
            if (await client.IsRecordingAsync(ct)) throw new Exception("Expected idle");
            using var start = await client.RequestAsync("StartRecord", ct);
            if (!await client.IsRecordingAsync(ct)) throw new Exception("Expected recording");
            using var stop = await client.RequestAsync("StopRecord", ct);
            if (await client.IsRecordingAsync(ct)) throw new Exception("Expected stopped");
            await server;
        } finally { timeout.Cancel(); listener.Stop(); try { await server; } catch when (ct.IsCancellationRequested) { } }
    }
    private static Task Send(WebSocket socket, string text, CancellationToken ct) => socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, ct);
    private static async Task<string> Receive(WebSocket socket, CancellationToken ct)
    {
        using var stream = new MemoryStream(); byte[] buffer = new byte[8192]; WebSocketReceiveResult r;
        do { r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct); stream.Write(buffer, 0, r.Count); } while (!r.EndOfMessage);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
