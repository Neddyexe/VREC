using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Vrec;

namespace Vrec.Windows;
public sealed class Settings
{
    public int Port { get; set; } = 4455;
    public int PollMilliseconds { get; set; } = 1500;
    public double Threshold { get; set; } = .96;
    public double Margin { get; set; } = .03;
    public int StartConfirmations { get; set; } = 2;
    public int EndConfirmations { get; set; } = 3;
    public double PostMatchSeconds { get; set; } = 8;
    public bool StartOnLoading { get; set; }
    public List<VisualTemplate> Templates { get; set; } = [];
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VREC");
    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public static Settings Load()
    {
        if (!File.Exists(FilePath)) return new();
        var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("Empty settings");
        s.Validate(); return s;
    }
    public void Validate()
    {
        if (Port is < 1 or > 65535 || PollMilliseconds is < 500 or > 10000 || Threshold is < .8 or > 1 || Margin is < 0 or > .2 || StartConfirmations is < 1 or > 20 || EndConfirmations is < 1 or > 20 || PostMatchSeconds is < 0 or > 120) throw new InvalidDataException("Invalid settings ranges");
        foreach (var t in Templates)
            if (t.X < 0 || t.Y < 0 || t.Width <= 0 || t.Height <= 0 || t.X + t.Width > 1.001 || t.Y + t.Height > 1.001 || t.Pixels.Length != 1728 || !Enum.IsDefined(t.State)) throw new InvalidDataException("Invalid template");
    }
    public void Save()
    {
        Validate(); Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
}
// Windows Credential Manager protects the local OBS password under the current Windows account.
internal static class PasswordStore
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags, Type; public string TargetName; public string? Comment;
        public long LastWritten; public uint CredentialBlobSize; public IntPtr CredentialBlob;
        public uint Persist, AttributeCount; public IntPtr Attributes; public string? TargetAlias, UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Write(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Read(string target, uint type, uint flags, out IntPtr pointer);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr pointer);
    public static string Load()
    {
        if (!Read("VREC/OBS", 1, 0, out var ptr))
        {
            if (Marshal.GetLastWin32Error() == 1168) return "";
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        try { var c = Marshal.PtrToStructure<Credential>(ptr); return Marshal.PtrToStringUni(c.CredentialBlob, (int)c.CredentialBlobSize / 2) ?? ""; }
        finally { CredFree(ptr); }
    }
    public static void Save(string password)
    {
        if (Encoding.Unicode.GetByteCount(password) > 2560) throw new InvalidDataException("Password too long");
        var blob = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var c = new Credential { Type = 1, TargetName = "VREC/OBS", UserName = "OBS", CredentialBlobSize = (uint)Encoding.Unicode.GetByteCount(password), CredentialBlob = blob, Persist = 2 };
            if (!Write(ref c, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(blob); }
    }
}
