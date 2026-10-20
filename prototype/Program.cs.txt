using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// VREC initial prototype: OBS remote recording control, one file per VALORANT launch session.
// Does not inspect/inject game memory or claim to recognise individual matches.
Console.Title = "VREC - VALORANT Session Recorder";
Console.WriteLine("VREC | VALORANT session recording via OBS (prototype)");
Console.WriteLine("Keep OBS open with WebSocket server enabled on localhost:4455.");
Console.WriteLine("This version records GAME SESSIONS, not individual matches.");
Console.WriteLine("For your safety, only connect to OBS on your own computer.\n");
Console.Write("OBS WebSocket password (not saved; Enter if authentication disabled): ");
string password = ReadSecret();
Console.WriteLine("\nMonitoring VALORANT. Ctrl+C closes VREC; recording is stopped first if possible.");

var client = new ObsClient("ws://127.0.0.1:4455/", password);
bool isRecordingOurs = false;
bool wasGameRunning = false;
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
try
{
    while (!cancel.IsCancellationRequested)
    {
        using var gameProcessProbe = new ProcessProbe("VALORANT-Win64-Shipping");
        bool gameRunning = gameProcessProbe.Found;
        if (gameRunning && !wasGameRunning)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] VALORANT detected. Connecting to OBS...");
            try
            {
                await client.ConnectAsync(cancel.Token);
                // Never interrupt a recording that the user already started manually.
                bool alreadyRecording = await client.IsRecordingAsync(cancel.Token);
                if (alreadyRecording) Console.WriteLine("OBS is already recording; leaving it alone.");
                else
                {
                    await client.RequestAsync("StartRecord", cancel.Token);
                    isRecordingOurs = true;
                    Console.WriteLine("Recording started. Video goes to the OBS recording folder.");
                }
            }
            catch (Exception ex) { Console.WriteLine($"Could not start recording: {ex.Message}"); }
        }
        else if (!gameRunning && wasGameRunning && isRecordingOurs)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] VALORANT closed. Stopping recording...");
            try { await client.ConnectAsync(cancel.Token); await client.RequestAsync("StopRecord", cancel.Token); Console.WriteLine("Recording saved by OBS."); }
            catch (Exception ex) { Console.WriteLine($"Could not stop recording: {ex.Message}; please check OBS."); }
            isRecordingOurs = false;
        }
        wasGameRunning = gameRunning;
        await Task.Delay(2000, cancel.Token);
    }
}
catch (OperationCanceledException) { }
finally
{
    if (isRecordingOurs)
    {
        Console.WriteLine("Stopping VREC-owned recording...");
        try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await client.ConnectAsync(timeout.Token); await client.RequestAsync("StopRecord", timeout.Token); }
        catch { Console.WriteLine("Please stop the recording in OBS manually."); }
    }
    await client.DisposeAsync();
}

static string ReadSecret()
{
    var sb = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; continue; }
        if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
    }
    return sb.ToString();
}

sealed class ObsClient : IAsyncDisposable
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
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public ValueTask DisposeAsync() { socket?.Dispose(); return ValueTask.CompletedTask; }
}

sealed class ProcessProbe : IDisposable
{
    private readonly Process[] processes;
    public bool Found => processes.Length > 0;
    public ProcessProbe(string name) => processes = Process.GetProcessesByName(name);
    public void Dispose() { foreach (var p in processes) p.Dispose(); }
}
