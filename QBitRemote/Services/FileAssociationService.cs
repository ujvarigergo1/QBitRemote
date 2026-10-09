using Microsoft.Win32;

namespace QBitRemote.Services;

public static class FileAssociationService
{
    private const string Extension = ".torrent";
    private const string ProgId = "QBitRemote.torrent";
    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.torrent");
        return string.Equals(key?.GetValue("") as string, ProgId, StringComparison.OrdinalIgnoreCase);
    }

    public static void Register()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException(LocalizationService.Instance["ExecutablePathUnknown"]);
        using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes")!;
        using (var extension = classes.CreateSubKey(Extension)!) extension.SetValue("", ProgId);
        using (var app = classes.CreateSubKey(ProgId)!)
        {
            app.SetValue("", LocalizationService.Instance["TorrentFileType"]);
            app.SetValue("FriendlyTypeName", LocalizationService.Instance["TorrentFileType"]);
        }
        using (var icon = classes.CreateSubKey(ProgId + @"\DefaultIcon")!) icon.SetValue("", $"\"{executable}\",0");
        using (var command = classes.CreateSubKey(ProgId + @"\shell\open\command")!) command.SetValue("", $"\"{executable}\" \"%1\"");
        NotifyShell();
    }

    public static void Unregister()
    {
        using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes")!;
        using (var extension = classes.OpenSubKey(Extension, writable: true))
            if (extension is not null && string.Equals(extension.GetValue("") as string, ProgId, StringComparison.OrdinalIgnoreCase)) extension.DeleteValue("", throwOnMissingValue: false);
        classes.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false);
        NotifyShell();
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
    private static void NotifyShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
}
