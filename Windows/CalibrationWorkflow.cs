namespace Vrec.Windows;

// Keep the modal owner visible to WinForms: Hide() terminates ShowDialog(),
// allowing its caller's using block to dispose it while an async event is pending.
internal static class CalibrationWorkflow
{
    internal static async Task RunAsync(Form owner, Func<CancellationToken, Task> capture)
    {
        if (owner.IsDisposed || owner.Disposing) return;
        using var cancellation = new CancellationTokenSource();
        void Cancel(object? sender, EventArgs e) => cancellation.Cancel();
        owner.FormClosed += Cancel;
        owner.Disposed += Cancel;
        double opacity = owner.Opacity;
        try {
            owner.Opacity = 0;
            await capture(cancellation.Token);
        }
        finally {
            owner.FormClosed -= Cancel;
            owner.Disposed -= Cancel;
            if (!owner.IsDisposed && !owner.Disposing && !cancellation.IsCancellationRequested)
                owner.Opacity = opacity;
        }
    }
}
