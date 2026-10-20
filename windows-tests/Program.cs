using Vrec.Windows;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try {
            Run("modal owner survives capture and focus loss", async owner => {
                bool completed = false;
                await CalibrationWorkflow.RunAsync(owner, async ct => {
                    using var other = new Form();
                    other.Show(); other.Activate();
                    await Task.Delay(50, ct);
                    Check(owner.Modal && owner.Visible && !owner.IsDisposed);
                    completed = true;
                });
                Check(completed && owner.Opacity == 1 && owner.Modal);
            });
            Run("closing owner cancels countdown", async owner => {
                bool captured = false;
                var task = CalibrationWorkflow.RunAsync(owner, async ct => {
                    await Task.Delay(500, ct);
                    captured = true;
                });
                owner.Close();
                try { await task; throw new Exception("Expected cancellation"); }
                catch (OperationCanceledException) { }
                Check(!captured && !owner.Visible);
            });
            Run("disposing owner cancels pending continuation", async owner => {
                var task = CalibrationWorkflow.RunAsync(owner, ct => Task.Delay(500, ct));
                owner.Dispose();
                try { await task; throw new Exception("Expected cancellation"); }
                catch (OperationCanceledException) { }
                Check(owner.IsDisposed);
            });
            Run("failure restores owner and permits another capture", async owner => {
                try {
                    await CalibrationWorkflow.RunAsync(owner, _ => throw new IOException("capture failed"));
                    throw new Exception("Expected failure");
                } catch (IOException) { }
                Check(owner.Opacity == 1 && owner.Modal);
                await CalibrationWorkflow.RunAsync(owner, _ => Task.CompletedTask);
                Check(owner.Opacity == 1);
            });
            Run("owner closes while selection dialog is open", async owner => {
                await CalibrationWorkflow.RunAsync(owner, ct => {
                    using var selection = new Form();
                    using var timer = new System.Windows.Forms.Timer { Interval = 50 };
                    using var registration = ct.Register(() => selection.Close());
                    timer.Tick += (_, _) => { timer.Stop(); owner.Close(); };
                    timer.Start();
                    selection.ShowDialog(owner);
                    Check(ct.IsCancellationRequested);
                    return Task.CompletedTask;
                });
                await Task.Delay(50); // Let the owner finish its pending modal close.
                Check(!owner.Visible);
            });
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static void Run(string name, Func<Form, Task> test)
    {
        Console.WriteLine($"RUN {name}");
        using var owner = new Form();
        Exception? failure = null;
        bool finished = false;
        using var watchdog = new System.Windows.Forms.Timer { Interval = 10000 };
        watchdog.Tick += (_, _) => {
            Console.Error.WriteLine($"TIMEOUT {name}");
            Environment.Exit(1);
        };
        owner.Shown += async (_, _) => {
            try { await test(owner); }
            catch (Exception e) { failure = e; }
            finally {
                finished = true;
                if (!owner.IsDisposed) owner.Close();
                Application.ExitThread();
            }
        };
        // The outer pump keeps async continuations alive even after ShowDialog returns.
        using var launcher = new System.Windows.Forms.Timer { Interval = 10 };
        launcher.Tick += (_, _) => {
            launcher.Stop();
            watchdog.Start();
            owner.ShowDialog();
        };
        launcher.Start();
        Application.Run();
        if (failure is not null) throw failure;
        Check(finished);
        Console.WriteLine($"PASS {name}");
    }
    private static void Check(bool condition, [System.Runtime.CompilerServices.CallerArgumentExpression("condition")] string? expression = null)
    {
        if (!condition) throw new Exception($"Lifecycle assertion failed: {expression}");
    }
}
