using Microsoft.Win32;
using System.Text.Json;
using Vrec;
namespace Vrec.Windows;
internal sealed class SettingsForm : Form
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public SettingsForm(Settings current)
    {
        var settings = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(Settings.Load()))!;
        Text = "VREC settings — save, then restart VREC to apply"; Width = 640; Height = 580;
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12) }; Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "OBS settings are preserved. Keep OBS open with Game Capture configured.", AutoSize = true });
        var password = new TextBox { Width = 350, UseSystemPasswordChar = true, Text = PasswordStore.Load() };
        layout.Controls.Add(new Label { Text = "OBS WebSocket password (Windows Credential Manager)", AutoSize = true }); layout.Controls.Add(password);
        NumericUpDown Number(string title, decimal value, decimal min, decimal max, int decimals = 0) {
            layout.Controls.Add(new Label { Text = title, AutoSize = true }); var n = new NumericUpDown { Minimum = min, Maximum = max, Value = value, DecimalPlaces = decimals, Width = 160, Increment = decimals > 0 ? .01m : 1 }; layout.Controls.Add(n); return n;
        }
        var port = Number("Local OBS port", settings.Port, 1, 65535);
        var poll = Number("Detection interval (milliseconds)", settings.PollMilliseconds, 500, 10000);
        var threshold = Number("Similarity threshold (higher is stricter)", (decimal)settings.Threshold, .8m, 1m, 2);
        var buffer = Number("Post-match safety buffer (seconds)", (decimal)settings.PostMatchSeconds, 0, 120);
        var early = new CheckBox { Text = "Start on confirmed MATCH_LOADING (includes loading footage)", Checked = settings.StartOnLoading, AutoSize = true }; layout.Controls.Add(early);
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        var startup = new CheckBox { Text = "Start with Windows", Checked = key?.GetValue("VREC") is not null, AutoSize = true }; layout.Controls.Add(startup);
        var states = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250, DataSource = Enum.GetValues<MatchState>().Where(x => x is not MatchState.Unknown and not MatchState.GameClosed).ToArray() }; layout.Controls.Add(states);
        var count = new Label { Text = $"{settings.Templates.Count} calibrated templates", AutoSize = true }; layout.Controls.Add(count);
        var capture = new Button { Text = "Capture template in 4 seconds", AutoSize = true }; layout.Controls.Add(capture);
        capture.Click += async (_, _) => {
            if (!capture.Enabled) return;
            var state = (MatchState)states.SelectedItem!;
            capture.Enabled = false;
            try {
                await CalibrationWorkflow.RunAsync(this, async ct => {
                    await Task.Delay(4000, ct);
                    ct.ThrowIfCancellationRequested();
                    using var image = ScreenDetector.Capture();
                    if (image is null) {
                        if (!ct.IsCancellationRequested && !IsDisposed && !Disposing)
                            MessageBox.Show(this, "Put VALORANT in the foreground during the countdown. Use borderless window mode.");
                        return;
                    }
                    ct.ThrowIfCancellationRequested();
                    using var calibration = new CalibrationForm(image, state);
                    using var cancel = ct.Register(() => {
                        if (!calibration.IsDisposed) calibration.Close();
                    });
                    if (calibration.ShowDialog(this) == DialogResult.OK &&
                        !ct.IsCancellationRequested && !IsDisposed && !Disposing) {
                        settings.Templates.Add(calibration.Result!);
                        count.Text = $"{settings.Templates.Count} calibrated templates";
                    }
                });
            } catch (OperationCanceledException) {
                // Closing Settings cancels capture; it must never reopen the form.
            } catch {
                if (!IsDisposed && !Disposing)
                    MessageBox.Show(this, "Screen capture failed. Try borderless window mode.");
            } finally {
                if (!IsDisposed && !Disposing) capture.Enabled = true;
            }
        };
        var clear = new Button { Text = "Clear all templates", AutoSize = true }; clear.Click += (_, _) => { settings.Templates.Clear(); count.Text = "0 calibrated templates"; }; layout.Controls.Add(clear);
        var save = new Button { Text = "Save settings", AutoSize = true }; layout.Controls.Add(save);
        save.Click += (_, _) => {
            try {
                settings.Port = (int)port.Value; settings.PollMilliseconds = (int)poll.Value; settings.Threshold = (double)threshold.Value;
                settings.PostMatchSeconds = (double)buffer.Value; settings.StartOnLoading = early.Checked;
                PasswordStore.Save(password.Text); settings.Save();
                using var run = Registry.CurrentUser.CreateSubKey(RunKey);
                if (startup.Checked) run.SetValue("VREC", $"\"{Environment.ProcessPath}\""); else run.DeleteValue("VREC", false);
                MessageBox.Show("Saved. Restart VREC after the current match to apply changes."); Close();
            } catch { MessageBox.Show("Could not save settings or Windows startup registration. Check access permissions."); }
        };
    }
}
