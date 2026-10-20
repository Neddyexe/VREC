namespace Vrec;

public enum MatchState { Unknown, GameClosed, GameMenu, Matchmaking, AgentSelect, MatchLoading, MatchActive, MatchEnding, PostMatch }
public interface IRecorder
{
    Task<bool> IsRecordingAsync(CancellationToken ct);
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
public sealed record MatchOptions(int StartConfirmations = 2, int EndConfirmations = 3,
    double PostMatchSeconds = 8, bool StartOnLoading = false);

// Called serially, using monotonic elapsed time. Unknown evidence never cuts footage.
public sealed class MatchController(IRecorder recorder, MatchOptions options)
{
    private MatchState candidate = MatchState.Unknown;
    private int count;
    private TimeSpan? endingAt;
    private bool wanted;
    private bool externalRecording;
    public bool OwnsRecording { get; private set; }
    public bool WantsRecording => wanted;
    public MatchState State { get; private set; }
    public string Status { get; private set; } = "Idle";
    public async Task TickAsync(MatchState observation, TimeSpan now, CancellationToken ct)
    {
        if (observation == candidate) count++; else { candidate = observation; count = 1; }
        if (observation == MatchState.MatchActive || (options.StartOnLoading && observation == MatchState.MatchLoading))
        {
            endingAt = null;
            if (count >= options.StartConfirmations) { wanted = true; State = observation; }
        }
        else if (observation is MatchState.GameClosed or MatchState.PostMatch or MatchState.GameMenu or MatchState.MatchEnding)
        {
            if (count >= options.EndConfirmations)
            {
                State = observation;
                endingAt ??= now;
                if (now - endingAt >= TimeSpan.FromSeconds(options.PostMatchSeconds)) wanted = false;
            }
        }
        else
        {
            // A lost/occluded image or queue signal is not proof the match ended.
            endingAt = null;
            if (!wanted && count >= options.StartConfirmations) State = observation;
        }
        if (!wanted && !OwnsRecording)
        {
            Status = observation == MatchState.GameClosed ? "Idle" : "Waiting for Match";
            return;
        }
        try
        {
            bool recording = await recorder.IsRecordingAsync(ct);
            if (!recording) { OwnsRecording = false; externalRecording = false; }
            else if (!OwnsRecording) externalRecording = true;
            if (wanted && !recording)
            {
                await recorder.StartAsync(ct);
                OwnsRecording = true;
                Status = "Recording";
            }
            else if (!wanted && OwnsRecording)
            {
                Status = "Saving Recording";
                await recorder.StopAsync(ct);
                OwnsRecording = false;
                Status = "Waiting for Match";
            }
            else Status = OwnsRecording ? "Recording" : externalRecording ? "OBS recording externally; leaving it alone" : "Waiting for Match";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch
        {
            // On ambiguous responses/reconnect, never adopt or stop an unrelated recording.
            OwnsRecording = false;
            Status = "OBS unavailable or command unconfirmed; retrying (check OBS if already recording)";
            throw;
        }
    }
    public async Task ShutdownAsync(CancellationToken ct)
    {
        if (!OwnsRecording) return;
        await recorder.StopAsync(ct);
        OwnsRecording = false;
    }
}
