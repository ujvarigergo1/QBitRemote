using Microsoft.Win32;

namespace QBitRemote.Services;
public static class StartupService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled() { using var key = Registry.CurrentUser.OpenSubKey(KeyPath); return key?.GetValue("QBitRemote") is not null; }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath)!;
        if (enabled) key.SetValue("QBitRemote", $"\"{Environment.ProcessPath}\""); else key.DeleteValue("QBitRemote", false);
    }
}
