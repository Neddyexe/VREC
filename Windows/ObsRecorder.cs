using Vrec;
namespace Vrec.Windows;
internal sealed class ObsRecorder(int port, string password) : IRecorder, IAsyncDisposable
{
    private ObsClient client = new($"ws://127.0.0.1:{port}/", password);
    private async Task<T> Execute<T>(Func<ObsClient, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try { return await action(client, timeout.Token); }
        catch { await client.DisposeAsync(); client = new($"ws://127.0.0.1:{port}/", password); throw; }
    }
    public Task<bool> IsRecordingAsync(CancellationToken ct) => Execute((c, token) => c.IsRecordingAsync(token), ct);
    public Task StartAsync(CancellationToken ct) => Execute(async (c, token) => { using var response = await c.RequestAsync("StartRecord", token); return true; }, ct);
    public Task StopAsync(CancellationToken ct) => Execute(async (c, token) => { using var response = await c.RequestAsync("StopRecord", token); return true; }, ct);
    public Task<string> FolderAsync(CancellationToken ct) => Execute(async (c, token) => { using var response = await c.RequestAsync("GetRecordDirectory", token); return response.RootElement.GetProperty("d").GetProperty("responseData").GetProperty("recordDirectory").GetString()!; }, ct);
    public ValueTask DisposeAsync() => client.DisposeAsync();
}
