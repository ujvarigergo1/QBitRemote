using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QBitRemote.Models;

namespace QBitRemote.Services;
public sealed class SettingsService
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QBitRemote", "settings.json");
    public AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new(); } catch (Exception ex) { LogService.Write("Settings load failed", ex); return new(); }
    }
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static string? Protect(string? value) => string.IsNullOrEmpty(value) ? null : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    public static string Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser)); } catch { return ""; }
    }
}
