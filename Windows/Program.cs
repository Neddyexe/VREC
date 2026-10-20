using System.Diagnostics;
using Vrec;
using Vrec.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(true, @"Local\VREC.Tray", out bool first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        try { Application.Run(new TrayContext()); }
        catch { MessageBox.Show("VREC could not start. Check settings.json in %LOCALAPPDATA%\\VREC. Your OBS settings have not been changed.", "VREC"); }
    }
}
internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon tray;
    private readonly ToolStripMenuItem status = new("Starting…") { Enabled = false };
    private readonly CancellationTokenSource stop = new();
    private readonly Control dispatch = new();
    private readonly Settings settings;
    private readonly Task worker;
    private string lastStatus = "";
    private bool closing;
    public TrayContext()
    {
        settings = Settings.Load(); dispatch.CreateControl();
        var menu = new ContextMenuStrip(); menu.Items.Add(status);
        menu.Items.Add("Settings / calibrate", null, (_, _) => { using var form = new SettingsForm(settings); form.ShowDialog(); });
        menu.Items.Add("Open recording folder", null, async (_, _) => {
            try { await using var obs = new ObsRecorder(settings.Port, PasswordStore.Load()); using var ct = new CancellationTokenSource(6000); var folder = await obs.FolderAsync(ct.Token); if (Directory.Exists(folder)) Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
            catch { MessageBox.Show("Could not read the recording folder from OBS. Check that OBS is running."); }
        });
        menu.Items.Add("Open diagnostics", null, (_, _) => Process.Start(new ProcessStartInfo(Settings.DirectoryPath) { UseShellExecute = true }));
        menu.Items.Add("Exit", null, async (_, _) => await QuitAsync());
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "VREC", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => { using var form = new SettingsForm(settings); form.ShowDialog(); };
        Directory.CreateDirectory(Settings.DirectoryPath);
        worker = Task.Run(async () => { try { await MonitorAsync(); } catch { Report("Error: monitoring stopped; check local credential/settings access and restart VREC."); } });
        Microsoft.Win32.SystemEvents.SessionEnding += SessionEnding;
        if (settings.Templates.Count == 0) tray.ShowBalloonTip(7000, "VREC needs calibration", "Open Settings to capture match and post-match UI templates. Recording will not start on game launch.", ToolTipIcon.Info);
    }
    private void Report(string message)
    {
        if (lastStatus == message) return; lastStatus = message;
        try {
            var log = Path.Combine(Settings.DirectoryPath, "diagnostics.log");
            if (File.Exists(log) && new FileInfo(log).Length > 1024 * 1024) File.Move(log, log + ".previous", true);
            File.AppendAllText(log, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        } catch { /* Logging failure must not stop recording. */ }
        if (!dispatch.IsDisposed) dispatch.BeginInvoke(() => { status.Text = message; tray.Text = message.Length > 63 ? message[..63] : message; });
    }
    private async Task MonitorAsync()
    {
        await using var obs = new ObsRecorder(settings.Port, PasswordStore.Load());
        var controller = new MatchController(obs, new(settings.StartConfirmations, settings.EndConfirmations, settings.PostMatchSeconds, settings.StartOnLoading));
        var clock = Stopwatch.StartNew(); int failures = 0;
        TimeSpan nextHealthCheck = TimeSpan.Zero; bool? obsConnected = null;
        try {
            while (!stop.IsCancellationRequested) {
                try {
                    var observed = ScreenDetector.Observe(settings);
                    bool usedObs = controller.WantsRecording || controller.OwnsRecording;
                    await controller.TickAsync(observed, clock.Elapsed, stop.Token);
                    if (usedObs || controller.OwnsRecording) obsConnected = true;
                    if (!controller.WantsRecording && !controller.OwnsRecording && clock.Elapsed >= nextHealthCheck)
                    {
                        nextHealthCheck = clock.Elapsed + TimeSpan.FromSeconds(15);
                        try { await obs.IsRecordingAsync(stop.Token); obsConnected = true; }
                        catch (OperationCanceledException) when (stop.IsCancellationRequested) { throw; }
                        catch { obsConnected = false; }
                    }
                    failures = 0;
                    Report($"OBS {(obsConnected == true ? "connected" : obsConnected == false ? "disconnected" : "unknown")} | {observed} | {controller.Status}" + (settings.Templates.Count == 0 ? " | Calibration required" : ""));
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                catch { obsConnected = false; failures = Math.Min(failures + 1, 5); Report("Error: OBS/detection unavailable; check OBS and calibration. Retrying."); }
                await Task.Delay(failures == 0 ? settings.PollMilliseconds : Math.Min(15000, 1000 * (1 << failures)), stop.Token);
            }
        } catch (OperationCanceledException) { }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            try { await controller.ShutdownAsync(timeout.Token); }
            catch { Report("Shutdown could not confirm recording stop; check OBS manually."); }
        }
    }
    private void SessionEnding(object? sender, Microsoft.Win32.SessionEndingEventArgs e)
    {
        stop.Cancel();
        try { worker.Wait(TimeSpan.FromSeconds(7)); } catch { }
    }
    private async Task QuitAsync()
    {
        if (closing) return; closing = true; stop.Cancel();
        try { await worker; } catch { MessageBox.Show("VREC stopped with an error. Check OBS for an ongoing recording."); }
        Microsoft.Win32.SystemEvents.SessionEnding -= SessionEnding;
        tray.Visible = false; tray.Dispose(); dispatch.Dispose(); stop.Dispose(); ExitThread();
    }
}
