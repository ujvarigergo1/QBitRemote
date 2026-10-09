namespace QBitRemote.Services;
public static class LogService
{
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QBitRemote", "logs", $"qbitremote-{DateTime.Today:yyyyMMdd}.log");
    public static void Write(string message, Exception? ex = null)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); File.AppendAllText(FilePath, $"{DateTime.Now:O} {message}{(ex is null ? "" : " " + ex)}{Environment.NewLine}"); } catch { }
    }
}
