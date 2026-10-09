using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using QBitRemote.Models;
using QBitRemote.Services;
using QBitRemote.ViewModels;

namespace QBitRemote.Views;
public partial class TorrentDetailsWindow : Window
{
    private readonly MainViewModel _main; private readonly TorrentInfo _torrent;
    public DetailsModel Model { get; }
    public TorrentDetailsWindow(MainViewModel main, TorrentInfo torrent)
    { InitializeComponent(); _main = main; _torrent = torrent; Model = new DetailsModel(torrent); DataContext = Model; Loaded += LoadAsync; }
    private async void LoadAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            using var properties = await _main.TorrentDataAsync("/api/v2/torrents/properties", _torrent); AddUsefulProperties(properties.RootElement, Model.General);
            using var files = await _main.TorrentDataAsync("/api/v2/torrents/files", _torrent); foreach (var f in files.RootElement.EnumerateArray()) Model.Files.Add(new DetailFileRow(f.GetProperty("name").GetString() ?? "", MainViewModel.FormatSize(Long(f, "size")), $"{(f.TryGetProperty("progress", out var p) ? p.GetDouble() : 0):P0}", Priority(f.TryGetProperty("priority", out var priority) ? priority.GetInt32() : 1)));
            using var trackers = await _main.TorrentDataAsync("/api/v2/torrents/trackers", _torrent); foreach (var t in trackers.RootElement.EnumerateArray()) Model.Trackers.Add(new TrackerRow(t.TryGetProperty("url", out var url) ? url.GetString() ?? "" : "", TrackerStatus(t.TryGetProperty("status", out var status) ? status.GetInt32() : -1), t.TryGetProperty("msg", out var msg) ? msg.GetString() ?? "" : ""));
            using var peers = await _main.TorrentDataAsync("/api/v2/sync/torrentPeers", _torrent); if (peers.RootElement.TryGetProperty("peers", out var peerList)) foreach (var p in peerList.EnumerateObject()) { var peer = p.Value; Model.Peers.Add(new PeerRow(p.Name, peer.TryGetProperty("client", out var c) ? c.GetString() ?? "" : "", $"{(peer.TryGetProperty("progress", out var pr) ? pr.GetDouble() : 0):P0}", MainViewModel.FormatSpeed(Long(peer, "dl_speed")), MainViewModel.FormatSpeed(Long(peer, "up_speed")), peer.TryGetProperty("flags", out var fl) ? fl.GetString() ?? "" : "")); }
            var l = LocalizationService.Instance;
            Model.Connection.Add(new KeyValuePair<string, string>(l["CurrentState"], _torrent.StatusIcon + " " + _torrent.StatusText)); Model.Connection.Add(new KeyValuePair<string, string>(l["DownloadSpeed"], MainViewModel.FormatSpeed(_torrent.DownloadSpeed))); Model.Connection.Add(new KeyValuePair<string, string>(l["UploadSpeed"], MainViewModel.FormatSpeed(_torrent.UploadSpeed))); Model.Connection.Add(new KeyValuePair<string, string>(l["Category"], _torrent.Category)); Model.Connection.Add(new KeyValuePair<string, string>(l["Tags"], _torrent.Tags));
        }
        catch (Exception ex) { MessageBox.Show(this, string.Format(LocalizationService.Instance["DetailsLoadFailed"], ex.Message), "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private static long Long(JsonElement e, string property) => e.TryGetProperty(property, out var v) && v.TryGetInt64(out var n) ? n : 0;
    private static string Priority(int value) { var l = LocalizationService.Instance; return value switch { 0 => l["DoNotDownload"], 1 => l["Normal"], 6 => l["High"], 7 => l["Maximum"], _ => value.ToString() }; }
    private static string TrackerStatus(int value) { var l = LocalizationService.Instance; return value switch { 0 => l["Disabled"], 1 => l["NotContacted"], 2 => l["Working"], 3 => l["Updating"], 4 => l["NotWorking"], _ => l["Unknown"] }; }
    private static void AddUsefulProperties(JsonElement e, ICollection<KeyValuePair<string, string>> target)
    {
        var l = LocalizationService.Instance;
        var text = new Dictionary<string, string> { ["save_path"] = l["SavePath"], ["comment"] = l["Comment"], ["created_by"] = l["CreatedBy"] };
        var bytes = new Dictionary<string, string> { ["total_downloaded"] = l["Downloaded"], ["total_uploaded"] = l["Uploaded"], ["dl_speed"] = l["DownloadSpeed"], ["up_speed"] = l["UploadSpeed"] };
        var duration = new Dictionary<string, string> { ["time_elapsed"] = l["ActiveTime"], ["seeding_time"] = l["SeedingTime"], ["eta"] = l["Eta"] };
        var dates = new Dictionary<string, string> { ["addition_date"] = l["Added"], ["completion_date"] = l["Completed"], ["creation_date"] = l["Created"] };
        foreach (var (key, label) in text) if (e.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString())) target.Add(new(label, value.GetString()!));
        foreach (var (key, label) in bytes) if (e.TryGetProperty(key, out var value)) target.Add(new(label, key.EndsWith("speed") ? MainViewModel.FormatSpeed(Long(e, key)) : MainViewModel.FormatSize(Long(e, key))));
        foreach (var (key, label) in duration) if (e.TryGetProperty(key, out var value) && value.TryGetInt64(out var seconds)) target.Add(new(label, seconds < 0 ? l["Unknown"] : TimeSpan.FromSeconds(seconds).ToString("d\\.hh\\:mm\\:ss")));
        foreach (var (key, label) in dates) if (e.TryGetProperty(key, out var value) && value.TryGetInt64(out var unix) && unix > 0) target.Add(new(label, DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.ToString("yyyy-MM-dd HH:mm")));
        if (e.TryGetProperty("share_ratio", out var ratio)) target.Add(new(l["ShareRatio"], ratio.GetDouble().ToString("0.00")));
    }
}
public sealed class DetailsModel
{
    public DetailsModel(TorrentInfo torrent)
    {
        Name = torrent.Name; Hash = torrent.Hash; var l = LocalizationService.Instance;
        General = [new(l["Progress"], $"{torrent.Progress:P1}"), new(l["Size"], MainViewModel.FormatSize(torrent.Size)), new(l["Eta"], torrent.Eta < 0 ? l["Unknown"] : TimeSpan.FromSeconds(torrent.Eta).ToString())];
    }
    public string Name { get; } public string Hash { get; }
    public ObservableCollection<KeyValuePair<string, string>> General { get; }
    public ObservableCollection<KeyValuePair<string, string>> Connection { get; } = [];
    public ObservableCollection<DetailFileRow> Files { get; } = []; public ObservableCollection<PeerRow> Peers { get; } = []; public ObservableCollection<TrackerRow> Trackers { get; } = [];
}
public sealed record DetailFileRow(string Path, string Size, string Progress, string Priority); public sealed record PeerRow(string Ip, string Client, string Progress, string Down, string Up, string Flags); public sealed record TrackerRow(string Url, string Status, string Message);
