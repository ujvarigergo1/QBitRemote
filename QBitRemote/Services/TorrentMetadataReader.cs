using System.Text;
using QBitRemote.Models;

namespace QBitRemote.Services;
public sealed record TorrentMetadata(string Name, long TotalSize, IReadOnlyList<TorrentFileInfo> Files);
public static class TorrentMetadataReader
{
    public static TorrentMetadata Read(string path)
    {
        var root = new Parser(File.ReadAllBytes(path)).Value() as Dictionary<string, object?> ?? throw new InvalidDataException(LocalizationService.Instance["InvalidTorrentFile"]);
        if (!root.TryGetValue("info", out var infoObj) || infoObj is not Dictionary<string, object?>) throw new InvalidDataException(LocalizationService.Instance["TorrentInfoMissing"]);
        var info = (Dictionary<string, object?>)infoObj;
        var name = Text(info, "name") ?? Path.GetFileNameWithoutExtension(path);
        var files = new List<TorrentFileInfo>();
        if (info.TryGetValue("files", out var multiple) && multiple is List<object?> entries)
        {
            foreach (var entry in entries.OfType<Dictionary<string, object?>>())
            {
                IEnumerable<string> pieces = entry.TryGetValue("path", out var p) && p is List<object?> list ? list.Select(x => x as byte[]).Where(x => x is not null).Select(x => Encoding.UTF8.GetString(x!)) : Enumerable.Empty<string>();
                var parts = pieces.ToArray();
                files.Add(new TorrentFileInfo { Path = parts.Length == 0 ? LocalizationService.Instance["UnnamedFile"] : Path.Combine(parts), Size = Number(entry, "length") });
            }
        }
        else files.Add(new TorrentFileInfo { Path = name, Size = Number(info, "length") });
        return new TorrentMetadata(name, files.Sum(x => x.Size), files);
    }
    private static string? Text(Dictionary<string, object?> d, string key) => d.TryGetValue(key, out var o) && o is byte[] b ? Encoding.UTF8.GetString(b) : null;
    private static long Number(Dictionary<string, object?> d, string key) => d.TryGetValue(key, out var o) && o is long n ? n : 0;
    private sealed class Parser(byte[] data)
    {
        private int _i;
        public object? Value() => data[_i] switch { (byte)'d' => Dict(), (byte)'l' => List(), (byte)'i' => Integer(), >= (byte)'0' and <= (byte)'9' => Bytes(), _ => throw new InvalidDataException(LocalizationService.Instance["InvalidBencode"]) };
        private Dictionary<string, object?> Dict() { _i++; var d = new Dictionary<string, object?>(); while (data[_i] != 'e') d[Encoding.UTF8.GetString((byte[])Bytes()!)] = Value(); _i++; return d; }
        private List<object?> List() { _i++; var l = new List<object?>(); while (data[_i] != 'e') l.Add(Value()); _i++; return l; }
        private long Integer() { _i++; var s = _i; while (data[_i] != 'e') _i++; var v = long.Parse(Encoding.ASCII.GetString(data, s, _i - s), System.Globalization.CultureInfo.InvariantCulture); _i++; return v; }
        private object Bytes() { var s = _i; while (data[_i] != ':') _i++; var n = int.Parse(Encoding.ASCII.GetString(data, s, _i - s)); _i++; var b = data[_i..(_i + n)]; _i += n; return b; }
    }
}
