using System.Diagnostics;
using System.Runtime.InteropServices;
using Vrec;
namespace Vrec.Windows;
internal static class ScreenDetector
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X, Y; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClientToScreen(IntPtr hwnd, ref PointNative point);
    public static bool GameRunning()
    {
        var processes = Process.GetProcessesByName("VALORANT-Win64-Shipping");
        try { return processes.Length > 0; } finally { foreach (var p in processes) p.Dispose(); }
    }
    public static Bitmap? Capture()
    {
        var hwnd = GetForegroundWindow(); GetWindowThreadProcessId(hwnd, out uint pid);
        try { using var process = Process.GetProcessById((int)pid); if (!process.ProcessName.Equals("VALORANT-Win64-Shipping", StringComparison.OrdinalIgnoreCase)) return null; }
        catch (ArgumentException) { return null; }
        if (!GetClientRect(hwnd, out var r)) return null;
        var origin = new PointNative(); if (!ClientToScreen(hwnd, ref origin) || r.Right < 100 || r.Bottom < 100) return null;
        var bitmap = new Bitmap(r.Right, r.Bottom);
        try { using var g = Graphics.FromImage(bitmap); g.CopyFromScreen(origin.X, origin.Y, 0, 0, bitmap.Size); return bitmap; }
        catch { bitmap.Dispose(); throw; }
    }
    public static byte[] Sample(Bitmap image, double x, double y, double width, double height)
    {
        var result = new byte[1728]; int i = 0;
        for (int row = 0; row < 18; row++) for (int col = 0; col < 32; col++)
        {
            int px = Math.Clamp((int)((x + (col + .5) / 32 * width) * image.Width), 0, image.Width - 1);
            int py = Math.Clamp((int)((y + (row + .5) / 18 * height) * image.Height), 0, image.Height - 1);
            var c = image.GetPixel(px, py); result[i++] = c.R; result[i++] = c.G; result[i++] = c.B;
        }
        return result;
    }
    public static MatchState Observe(Settings settings)
    {
        if (!GameRunning()) return MatchState.GameClosed;
        if (settings.Templates.Count == 0) return MatchState.Unknown;
        using var bitmap = Capture(); if (bitmap is null) return MatchState.Unknown;
        return TemplateMatcher.Classify(settings.Templates.Select(t => (t.State, TemplateMatcher.Similarity(t.Pixels, Sample(bitmap, t.X, t.Y, t.Width, t.Height)))), settings.Threshold, settings.Margin);
    }
}
