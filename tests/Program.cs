using Vrec;

int passed = 0;
async Task Test(string name, Func<Task> action) { await action(); Console.WriteLine($"PASS {name}"); passed++; }
static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
await Test("Launch/menu/queue/select do not record", async () => {
    var r = new Fake(); var c = new MatchController(r, new());
    foreach (var s in new[] { MatchState.GameClosed, MatchState.GameMenu, MatchState.Matchmaking, MatchState.AgentSelect, MatchState.MatchLoading }) for (int i = 0; i < 5; i++) await c.TickAsync(s, TimeSpan.FromSeconds(i), default);
    Check(r.Starts == 0);
});
await Test("Debounce; unknown and overtime preserve recording; post buffer; separate next match", async () => {
    var r = new Fake(); var c = new MatchController(r, new(PostMatchSeconds: 2));
    await c.TickAsync(MatchState.MatchActive, TimeSpan.Zero, default); Check(r.Starts == 0);
    await c.TickAsync(MatchState.MatchActive, TimeSpan.FromSeconds(1), default); Check(r.Starts == 1);
    for (int i = 2; i < 50; i++) await c.TickAsync(MatchState.Unknown, TimeSpan.FromSeconds(i), default);
    Check(r.Stops == 0);
    for (int i = 50; i < 55; i++) await c.TickAsync(MatchState.PostMatch, TimeSpan.FromSeconds(i), default);
    Check(r.Stops == 1 && !r.Active);
    for (int i = 55; i < 60; i++) await c.TickAsync(MatchState.MatchActive, TimeSpan.FromSeconds(i), default);
    Check(r.Starts == 2);
});
await Test("Never stops external recording, including shutdown", async () => {
    var r = new Fake { Active = true }; var c = new MatchController(r, new(PostMatchSeconds: 0));
    for (int i = 0; i < 10; i++) await c.TickAsync(MatchState.GameClosed, TimeSpan.FromSeconds(i), default);
    await c.ShutdownAsync(default); Check(r.Stops == 0 && r.Starts == 0);
});
await Test("Failed start retries without new match edge", async () => {
    var r = new Fake { FailStart = true }; var c = new MatchController(r, new(StartConfirmations: 1));
    try { await c.TickAsync(MatchState.MatchActive, TimeSpan.Zero, default); } catch (IOException) { }
    r.FailStart = false; await c.TickAsync(MatchState.MatchActive, TimeSpan.FromSeconds(1), default); Check(r.Starts == 1);
});
await Test("Disconnect relinquishes ownership; active output remains untouched", async () => {
    var r = new Fake(); var c = new MatchController(r, new(StartConfirmations: 1, EndConfirmations: 1, PostMatchSeconds: 0));
    await c.TickAsync(MatchState.MatchActive, TimeSpan.Zero, default); r.FailStatus = true;
    try { await c.TickAsync(MatchState.Unknown, TimeSpan.FromSeconds(1), default); } catch (IOException) { }
    r.FailStatus = false; await c.TickAsync(MatchState.PostMatch, TimeSpan.FromSeconds(2), default); Check(r.Stops == 0);
});
await Test("Crash closes owned recording after debounce", async () => {
    var r = new Fake(); var c = new MatchController(r, new(StartConfirmations: 1, EndConfirmations: 2, PostMatchSeconds: 0));
    await c.TickAsync(MatchState.MatchActive, TimeSpan.Zero, default);
    await c.TickAsync(MatchState.GameClosed, TimeSpan.FromSeconds(1), default); Check(r.Stops == 0);
    await c.TickAsync(MatchState.GameClosed, TimeSpan.FromSeconds(2), default); Check(r.Stops == 1);
});
await Test("False ending cancels buffer; shutdown stops owned output", async () => {
    var r = new Fake(); var c = new MatchController(r, new(StartConfirmations: 1, EndConfirmations: 1, PostMatchSeconds: 8));
    await c.TickAsync(MatchState.MatchActive, TimeSpan.Zero, default);
    await c.TickAsync(MatchState.MatchEnding, TimeSpan.FromSeconds(1), default);
    await c.TickAsync(MatchState.MatchActive, TimeSpan.FromSeconds(10), default); Check(r.Stops == 0);
    await c.ShutdownAsync(default); Check(r.Stops == 1);
});
await Test("Loading early-start is explicit opt-in", async () => {
    var r = new Fake(); var c = new MatchController(r, new(StartConfirmations: 1, StartOnLoading: true));
    await c.TickAsync(MatchState.MatchLoading, TimeSpan.Zero, default); Check(r.Starts == 1);
});
await Test("OBS restart during active match resumes idle output", async () => {
    var r = new Fake(); var c = new MatchController(r, new(StartConfirmations: 1));
    await c.TickAsync(MatchState.MatchActive, TimeSpan.Zero, default);
    r.Active = false;
    await c.TickAsync(MatchState.Unknown, TimeSpan.FromSeconds(2), default);
    Check(r.Starts == 2 && c.OwnsRecording);
});
await Test("Unacknowledged start does not adopt an active recording", async () => {
    var r = new Fake { FailAfterStart = true }; var c = new MatchController(r, new(StartConfirmations: 1, EndConfirmations: 1, PostMatchSeconds: 0));
    try { await c.TickAsync(MatchState.MatchActive, TimeSpan.Zero, default); } catch (IOException) { }
    r.FailAfterStart = false;
    await c.TickAsync(MatchState.MatchActive, TimeSpan.FromSeconds(1), default);
    await c.TickAsync(MatchState.PostMatch, TimeSpan.FromSeconds(2), default);
    Check(r.Starts == 1 && r.Stops == 0 && !c.OwnsRecording);
});
await Test("Failed stop preserves footage without claiming success", async () => {
    var r = new Fake(); var c = new MatchController(r, new(StartConfirmations: 1, EndConfirmations: 1, PostMatchSeconds: 0));
    await c.TickAsync(MatchState.MatchActive, TimeSpan.Zero, default); r.FailStop = true;
    try { await c.TickAsync(MatchState.GameClosed, TimeSpan.FromSeconds(1), default); } catch (IOException) { }
    Check(r.Active && c.Status.Contains("unconfirmed") && !c.OwnsRecording);
});
await Test("Template score and ambiguity reject", () => {
    byte[] image = Enumerable.Repeat((byte)128, 1728).ToArray();
    Check(TemplateMatcher.Similarity(image, image) == 1);
    Check(TemplateMatcher.Classify([(MatchState.MatchActive, .99), (MatchState.GameMenu, .98)], .96, .03) == MatchState.Unknown);
    Check(TemplateMatcher.Classify([(MatchState.MatchActive, .99), (MatchState.GameMenu, .8)], .96, .03) == MatchState.MatchActive);
    return Task.CompletedTask;
});
await Test("Synthetic fixture records two separate matches", async () => {
    var r = new Fake(); var c = new MatchController(r, new());
    using var fixture = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures/two-matches.json")));
    foreach (var e in fixture.RootElement.EnumerateArray())
        await c.TickAsync(Enum.Parse<MatchState>(e.GetProperty("State").GetString()!), TimeSpan.FromSeconds(e.GetProperty("Seconds").GetDouble()), default);
    Check(r.Starts == 2 && r.Stops == 2);
});
await Test("OBS v5 authenticated handshake, events, fragmented responses and recording requests", ObsProtocolTest.RunAsync);
Console.WriteLine($"{passed} tests passed");
sealed class Fake : IRecorder
{
    public bool Active, FailStart, FailStatus, FailAfterStart, FailStop; public int Starts, Stops;
    public Task<bool> IsRecordingAsync(CancellationToken ct) => FailStatus ? throw new IOException() : Task.FromResult(Active);
    public Task StartAsync(CancellationToken ct) { if (FailStart) throw new IOException(); CheckInactive(); Active = true; Starts++; if (FailAfterStart) throw new IOException(); return Task.CompletedTask; }
    private void CheckInactive() { if (Active) throw new Exception("Duplicate start"); }
    public Task StopAsync(CancellationToken ct) { if (FailStop) throw new IOException(); if (!Active) throw new Exception("Duplicate stop"); Active = false; Stops++; return Task.CompletedTask; }
}
