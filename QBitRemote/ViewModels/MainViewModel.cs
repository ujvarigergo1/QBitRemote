using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using QBitRemote.Infrastructure;
using QBitRemote.Models;
using QBitRemote.Services;

namespace QBitRemote.ViewModels;
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly QBitClient _client = new(); private readonly SettingsService _settingsService = new(); private readonly CancellationTokenSource _stop = new();
    private AppSettings _settings; private long _rid, _freeSpaceOnDisk = -1, _downloadSpeed, _uploadSpeed; private DateTime _nextFreeSpaceCheck = DateTime.MinValue; private string _connection = "", _summary = "", _filterName = "", _filterStatus = "", _filterCategory = "", _filterTag = ""; private bool _showFilters, _updatingFilterOptions;
    public ObservableCollection<TorrentInfo> Torrents { get; } = [];
    public ICollectionView TorrentsView { get; }
    public ObservableCollection<string> StatusOptions { get; } = [""];
    public ObservableCollection<string> CategoryOptions { get; } = [""];
    public ObservableCollection<string> TagOptions { get; } = [""];
    public string Connection { get => _connection; private set => Set(ref _connection, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public AppSettings Settings => _settings;
    public LocalizationService L { get; } = LocalizationService.Instance;
    public string FilterName { get => _filterName; set { if (!_updatingFilterOptions && Set(ref _filterName, value)) RefreshFilter(); } }
    public string FilterStatus { get => _filterStatus; set { if (!_updatingFilterOptions && Set(ref _filterStatus, value)) RefreshFilter(); } }
    public string FilterCategory { get => _filterCategory; set { if (!_updatingFilterOptions && Set(ref _filterCategory, value)) RefreshFilter(); } }
    public string FilterTag { get => _filterTag; set { if (!_updatingFilterOptions && Set(ref _filterTag, value)) RefreshFilter(); } }
    public bool ShowFilters { get => _showFilters; set => Set(ref _showFilters, value); }
    public bool HasActiveFilters => !string.IsNullOrWhiteSpace(FilterName) || !string.IsNullOrWhiteSpace(FilterStatus) || !string.IsNullOrWhiteSpace(FilterCategory) || !string.IsNullOrWhiteSpace(FilterTag);
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand ToggleFiltersCommand { get; }
    public event Action? Refreshing;
    public event Action? TorrentsUpdated;
    public MainViewModel() { _settings = _settingsService.Load(); L.SetLanguage(_settings.Language); _connection = L["Disconnected"]; TorrentsView = CollectionViewSource.GetDefaultView(Torrents); TorrentsView.Filter = MatchesFilter; ApplyDefaultTorrentOrder(); RefreshCommand = new AsyncCommand(_ => RefreshAsync()); ToggleFiltersCommand = new AsyncCommand(_ => { ShowFilters = !ShowFilters; return Task.CompletedTask; }); }
    public void ApplyManualSort(string property, ListSortDirection direction)
    {
        if (TorrentsView is ListCollectionView view) view.CustomSort = null;
        TorrentsView.SortDescriptions.Clear();
        TorrentsView.SortDescriptions.Add(new SortDescription(property, direction));
    }
    private void ApplyDefaultTorrentOrder()
    {
        if (TorrentsView is ListCollectionView view) { view.SortDescriptions.Clear(); view.CustomSort = ActiveDownloadComparer.Instance; }
    }
    public void SaveLanguage(string? language) { _settings.Language = language; L.SetLanguage(language); FilterStatus = ""; for (var i = 0; i < Torrents.Count; i++) Torrents[i] = Torrents[i] with { }; UpdateFilterOptions(); _settingsService.Save(_settings); }
    public async Task<bool> StartAsync()
    {
        try { var password = SettingsService.Unprotect(_settings.ProtectedPassword); await _client.ConnectAsync(_settings.ServerUrl, _settings.Username, password, _stop.Token); RememberSuccessfulConnection(_settings.ServerUrl, _settings.Username, password); Connection = L["Connected"]; _ = PollAsync(); return true; }
        catch (Exception ex) { Connection = L["Disconnected"] + " — " + ex.Message; LogService.Write("Initial connection failed", ex); return false; }
    }
    private async Task PollAsync() { while (!_stop.IsCancellationRequested) { await RefreshAsync(); try { await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_settings.RefreshIntervalSeconds, 1, 60)), _stop.Token); } catch (OperationCanceledException) { } } }
    public async Task RefreshAsync()
    {
        Refreshing?.Invoke();
        try
        {
            using var data = await _client.MainDataAsync(_rid, _stop.Token); var root = data.RootElement; _rid = root.GetProperty("rid").GetInt64(); Connection = L["Connected"];
            if (root.TryGetProperty("torrents", out var changed)) foreach (var item in changed.EnumerateObject()) { var old = Torrents.FirstOrDefault(x => x.Hash == item.Name); var info = Parse(item.Name, item.Value, old); if (old is not null) Torrents[Torrents.IndexOf(old)] = info; else Torrents.Add(info); if (_settings.NotifyOnCompletion && old is not null && old.Progress < 1 && info.Progress >= 1) ((App)Application.Current).ShowNotification("QBitRemote", string.Format(L["CompletedNotification"], info.Name), _settings.PlayCompletionSound); }
            if (root.TryGetProperty("torrents_removed", out var removed)) foreach (var h in removed.EnumerateArray().Select(x => x.GetString())) { var item = Torrents.FirstOrDefault(x => x.Hash == h); if (item is not null) Torrents.Remove(item); }
            UpdateFilterOptions();
            if (root.TryGetProperty("server_state", out var state))
            {
                var freeSpace = GetLong(state, "free_space_on_disk", -1);
                if (freeSpace >= 0) _freeSpaceOnDisk = freeSpace;
                _downloadSpeed = GetLong(state, "dl_info_speed");
                _uploadSpeed = GetLong(state, "up_info_speed");
            }
            if (_freeSpaceOnDisk < 0 || DateTime.UtcNow >= _nextFreeSpaceCheck)
            {
                _nextFreeSpaceCheck = DateTime.UtcNow.AddMinutes(1);
                try { _freeSpaceOnDisk = await _client.GetDefaultSaveFreeSpaceAsync(_stop.Token) ?? _freeSpaceOnDisk; }
                catch (Exception ex) { LogService.Write("Could not get free disk space", ex); }
            }
            var spaceText = $"    {L["Free"]}: {(_freeSpaceOnDisk >= 0 ? FormatSize(_freeSpaceOnDisk) : L["FreeSpaceUnavailable"])}";
            Summary = $"{L["Download"]}: {FormatSpeed(_downloadSpeed)}    {L["Upload"]}: {FormatSpeed(_uploadSpeed)}{spaceText}    {L["Active"]}: {Torrents.Count(x => x.Status is "downloading" or "uploading" or "stalledDL" or "stalledUP")}";
            TorrentsUpdated?.Invoke();
        }
        catch (Exception ex) { Connection = L["Disconnected"] + " — " + ex.Message; LogService.Write("Refresh failed", ex); }
    }
    public async Task SaveSettingsAsync(string url, string user, string password) { url = url.Trim().TrimEnd('/'); user = user.Trim(); _rid = 0; await _client.ConnectAsync(url, user, password, _stop.Token); RememberSuccessfulConnection(url, user, password); Connection = L["Connected"]; await RefreshAsync(); }
    private void RememberSuccessfulConnection(string url, string user, string password)
    {
        var normalizedUrl = url.Trim().TrimEnd('/');
        var matches = _settings.ServerProfiles.Where(x => x.ServerUrl.Trim().TrimEnd('/').Equals(normalizedUrl, StringComparison.OrdinalIgnoreCase) && x.Username.Equals(user, StringComparison.OrdinalIgnoreCase)).ToList();
        var profile = matches.FirstOrDefault();
        var protectedPassword = SettingsService.Protect(password);
        if (profile is null) { profile = new ServerProfile { Name = normalizedUrl, ServerUrl = normalizedUrl, Username = user, ProtectedPassword = protectedPassword }; _settings.ServerProfiles.Add(profile); }
        else { profile.ServerUrl = normalizedUrl; profile.Username = user; profile.ProtectedPassword = protectedPassword; if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = normalizedUrl; foreach (var duplicate in matches.Skip(1)) _settings.ServerProfiles.Remove(duplicate); }
        _settings.ServerUrl = normalizedUrl; _settings.Username = user; _settings.ProtectedPassword = protectedPassword;
        _settingsService.Save(_settings);
    }
    public Task<(Dictionary<string, string>, List<string>)> LabelsAsync() => _client.GetLabelsAsync(_stop.Token);
    public Task CreateCategoryAsync(string category, string savePath) => _client.CreateCategoryAsync(category, savePath, _stop.Token);
    public Task EditCategoryAsync(string category, string savePath) => _client.EditCategoryAsync(category, savePath, _stop.Token);
    public Task DeleteCategoryAsync(string category) => _client.DeleteCategoryAsync(category, _stop.Token);
    public async Task AddAsync(string f, string torrentName, string path, string category, IEnumerable<string> tags, bool paused, IReadOnlyList<TorrentFileInfo> files) { await _client.AddTorrentAsync(f, torrentName, path, category, tags, paused, files, _stop.Token); if (!string.IsNullOrWhiteSpace(path)) { _settings.RecentSavePaths.Remove(path); _settings.RecentSavePaths.Insert(0, path); _settings.RecentSavePaths = _settings.RecentSavePaths.Take(15).ToList(); _settingsService.Save(_settings); } await RefreshAsync(); }
    public Task PauseAsync(TorrentInfo t) => _client.PauseAsync(t.Hash, _stop.Token); public Task ResumeAsync(TorrentInfo t) => _client.ResumeAsync(t.Hash, _stop.Token); public Task DeleteAsync(TorrentInfo t, bool files) => _client.DeleteAsync(t.Hash, files, _stop.Token);
    public Task RecheckAsync(TorrentInfo t) => _client.RecheckAsync(t.Hash, _stop.Token);
    public Task ReannounceAsync(TorrentInfo t) => _client.ReannounceAsync(t.Hash, _stop.Token);
    public Task ForceStartAsync(TorrentInfo t, bool enabled = true) => _client.ForceStartAsync(t.Hash, enabled, _stop.Token);
    public Task SetFilePriorityAsync(TorrentInfo t, string ids, int priority) => _client.FilePriorityAsync(t.Hash, ids, priority, _stop.Token);
    public Task SetLocationAsync(TorrentInfo t, string path) => _client.SetLocationAsync(t.Hash, path, _stop.Token);
    public Task SetCategoryAsync(TorrentInfo t, string category) => _client.SetCategoryAsync(t.Hash, category, _stop.Token);
    public Task AddTagsAsync(TorrentInfo t, string tags) => _client.AddTagsAsync(t.Hash, tags, _stop.Token);
    public Task RemoveTagsAsync(TorrentInfo t, string tags) => _client.RemoveTagsAsync(t.Hash, tags, _stop.Token);
    public Task<JsonDocument> TorrentDataAsync(string endpoint, TorrentInfo t) => _client.GetTorrentDataAsync(endpoint, t.Hash, _stop.Token);
    private bool MatchesFilter(object item)
    {
        if (item is not TorrentInfo t) return false;
        return (string.IsNullOrWhiteSpace(FilterName) || t.Name.Contains(FilterName, StringComparison.OrdinalIgnoreCase)) &&
               (string.IsNullOrWhiteSpace(FilterStatus) || t.StatusText.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase)) &&
               (string.IsNullOrWhiteSpace(FilterCategory) || t.Category.Equals(FilterCategory, StringComparison.OrdinalIgnoreCase)) &&
               (string.IsNullOrWhiteSpace(FilterTag) || t.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(FilterTag, StringComparer.OrdinalIgnoreCase));
    }
    private void RefreshFilter() { TorrentsView.Refresh(); Raise(nameof(HasActiveFilters)); }
    private void UpdateFilterOptions()
    {
        var status = FilterStatus; var category = FilterCategory; var tag = FilterTag;
        _updatingFilterOptions = true;
        try
        {
            UpdateOptions(StatusOptions, Torrents.Select(x => x.StatusText), status);
            UpdateOptions(CategoryOptions, Torrents.Select(x => x.Category), category);
            UpdateOptions(TagOptions, Torrents.SelectMany(x => x.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)), tag);
        }
        finally { _updatingFilterOptions = false; }
        // Clearing an ItemsSource makes ComboBox clear its own selection. The values above
        // were intentionally preserved, so raise them explicitly to restore that visual selection.
        Raise(nameof(FilterStatus)); Raise(nameof(FilterCategory)); Raise(nameof(FilterTag)); RefreshFilter();
    }
    private static void UpdateOptions(ObservableCollection<string> target, IEnumerable<string> values, string selected)
    {
        // Do not clear this collection: clearing makes WPF clear ComboBox.SelectedItem.
        var desired = new List<string> { "" };
        if (!string.IsNullOrWhiteSpace(selected)) desired.Add(selected);
        desired.AddRange(values.Where(value => !string.IsNullOrWhiteSpace(value) && !value.Equals(selected, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).Order());
        for (var index = target.Count - 1; index >= 0; index--) if (!desired.Contains(target[index], StringComparer.OrdinalIgnoreCase)) target.RemoveAt(index);
        foreach (var value in desired) if (!target.Contains(value, StringComparer.OrdinalIgnoreCase)) target.Add(value);
        for (var index = 0; index < desired.Count; index++)
        {
            var currentIndex = target.IndexOf(target.First(value => value.Equals(desired[index], StringComparison.OrdinalIgnoreCase)));
            if (currentIndex != index) target.Move(currentIndex, index);
        }
    }
    private static TorrentInfo Parse(string hash, JsonElement e, TorrentInfo? old) => new() { Hash = hash, Name = GetText(e, "name", old?.Name ?? ""), Progress = GetDouble(e, "progress", old?.Progress ?? 0), Status = GetText(e, "state", old?.Status ?? ""), DownloadSpeed = GetLong(e, "dlspeed", old?.DownloadSpeed ?? 0), UploadSpeed = GetLong(e, "upspeed", old?.UploadSpeed ?? 0), Eta = GetLong(e, "eta", old?.Eta ?? 0), Size = GetLong(e, "size", old?.Size ?? 0), AddedOn = GetUnixTime(e, "added_on", old?.AddedOn ?? DateTimeOffset.MinValue), Category = GetText(e, "category", old?.Category ?? ""), Tags = GetText(e, "tags", old?.Tags ?? "") };
    private static string GetText(JsonElement e, string name, string fallback) => e.TryGetProperty(name, out var value) ? value.GetString() ?? "" : fallback;
    private static double GetDouble(JsonElement e, string name, double fallback) => e.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? number : fallback;
    private static long GetLong(JsonElement e, string name) => GetLong(e, name, 0);
    private static long GetLong(JsonElement e, string name, long fallback) => e.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : fallback;
    private static DateTimeOffset GetUnixTime(JsonElement e, string name, DateTimeOffset fallback)
    {
        var seconds = GetLong(e, name, 0);
        if (seconds <= 0) return fallback;
        try { return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime(); }
        catch (ArgumentOutOfRangeException) { return fallback; }
    }
    public static string FormatSize(long b) { string[] u = ["B", "KB", "MB", "GB", "TB"]; double v = b; var i = 0; while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; } return $"{v:0.##} {u[i]}"; }
    public static string FormatSpeed(long b) => FormatSize(b) + "/s";
    public void Dispose() { _stop.Cancel(); _client.Dispose(); _stop.Dispose(); }

    private sealed class ActiveDownloadComparer : IComparer
    {
        public static ActiveDownloadComparer Instance { get; } = new();
        public int Compare(object? x, object? y)
        {
            if (x is not TorrentInfo left || y is not TorrentInfo right) return 0;
            var active = IsActiveDownload(right).CompareTo(IsActiveDownload(left));
            if (active != 0) return active;
            var speed = right.DownloadSpeed.CompareTo(left.DownloadSpeed);
            return speed != 0 ? speed : StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
        }
        private static bool IsActiveDownload(TorrentInfo torrent) => torrent.Status is "downloading" or "forcedDL" or "metaDL";
    }
}
