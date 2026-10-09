using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using QBitRemote.Models;

namespace QBitRemote.Services;
public sealed class QBitClient : IDisposable
{
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private string _baseUrl = "";
    private string _user = "", _password = "";
    public QBitClient() => _http = new HttpClient(new HttpClientHandler { CookieContainer = _cookies, UseCookies = true }) { Timeout = TimeSpan.FromSeconds(12) };
    public async Task ConnectAsync(string url, string user, string password, CancellationToken ct)
    {
        _baseUrl = url.TrimEnd('/'); _user = user; _password = password;
        await LoginAsync(ct);
    }
    private async Task LoginAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl)) throw new InvalidOperationException(LocalizationService.Instance["ServerUrlRequired"]);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = _user, ["password"] = _password });
        using var response = await _http.PostAsync(_baseUrl + "/api/v2/auth/login", content, ct);
        var result = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode || !result.Trim().Equals("Ok.", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException(LocalizationService.Instance["AuthenticationRejected"]);
    }
    private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> action, CancellationToken ct)
    {
        var response = await action();
        if (response.StatusCode != HttpStatusCode.Forbidden) return response;
        response.Dispose(); await LoginAsync(ct); return await action();
    }
    public async Task<(Dictionary<string, string> CategoryPaths, List<string> Tags)> GetLabelsAsync(CancellationToken ct)
    {
        using var cat = await SendAsync(() => _http.GetAsync(_baseUrl + "/api/v2/torrents/categories", ct), ct); cat.EnsureSuccessStatusCode();
        using var tag = await SendAsync(() => _http.GetAsync(_baseUrl + "/api/v2/torrents/tags", ct), ct); tag.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await cat.Content.ReadAsStringAsync(ct));
        var categories = doc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.TryGetProperty("savePath", out var path) ? path.GetString() ?? "" : "", StringComparer.OrdinalIgnoreCase);
        return (categories, (await tag.Content.ReadFromJsonAsync<List<string>>(cancellationToken: ct) ?? []).Order().ToList());
    }
    public async Task<JsonDocument> MainDataAsync(long rid, CancellationToken ct)
    {
        using var response = await SendAsync(() => _http.GetAsync($"{_baseUrl}/api/v2/sync/maindata?rid={rid}", ct), ct); response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    public async Task<long?> GetDefaultSaveFreeSpaceAsync(CancellationToken ct)
    {
        // A full sync is the most compatible way to obtain the value: some qBittorrent
        // versions omit server_state from incremental sync replies.
        try
        {
            using var mainData = await MainDataAsync(0, ct);
            if (mainData.RootElement.TryGetProperty("server_state", out var state) && state.TryGetProperty("free_space_on_disk", out var free) && free.TryGetInt64(out var fullSyncFreeSpace) && fullSyncFreeSpace >= 0) return fullSyncFreeSpace;
        }
        catch (Exception ex) { LogService.Write("Full sync disk-space lookup failed", ex); }

        string? savePath = null;
        using (var defaultPathResponse = await SendAsync(() => _http.GetAsync(_baseUrl + "/api/v2/app/defaultSavePath", ct), ct))
            if (defaultPathResponse.IsSuccessStatusCode) savePath = (await defaultPathResponse.Content.ReadAsStringAsync(ct)).Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(savePath))
        {
            using var preferencesResponse = await SendAsync(() => _http.GetAsync(_baseUrl + "/api/v2/app/preferences", ct), ct);
            if (preferencesResponse.IsSuccessStatusCode)
            {
                using var preferences = JsonDocument.Parse(await preferencesResponse.Content.ReadAsStringAsync(ct));
                if (preferences.RootElement.TryGetProperty("save_path", out var pathValue)) savePath = pathValue.GetString();
            }
        }
        if (string.IsNullOrWhiteSpace(savePath)) return null;

        var path = Uri.EscapeDataString(savePath);
        foreach (var endpoint in new[] { "getFreeSpace", "getFreeSpaceAtPath" })
        {
            using var spaceResponse = await SendAsync(() => _http.GetAsync($"{_baseUrl}/api/v2/app/{endpoint}?path={path}", ct), ct);
            if (!spaceResponse.IsSuccessStatusCode) continue;
            var value = (await spaceResponse.Content.ReadAsStringAsync(ct)).Trim().Trim('"');
            if (long.TryParse(value, out var freeSpace) && freeSpace >= 0) return freeSpace;
        }
        return null;
    }
    public async Task AddTorrentAsync(string file, string torrentName, string savePath, string category, IEnumerable<string> tags, bool paused, IReadOnlyList<TorrentFileInfo> files, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        await using var stream = File.OpenRead(file);
        form.Add(new StreamContent(stream), "torrents", Path.GetFileName(file));
        if (!string.IsNullOrWhiteSpace(savePath)) form.Add(new StringContent(savePath), "savepath");
        if (!string.IsNullOrWhiteSpace(category)) form.Add(new StringContent(category), "category");
        var tagCsv = string.Join(',', tags); if (!string.IsNullOrWhiteSpace(tagCsv)) form.Add(new StringContent(tagCsv), "tags");
        form.Add(new StringContent("true"), "paused");
        using var response = await SendAsync(() => _http.PostAsync(_baseUrl + "/api/v2/torrents/add", form, ct), ct); response.EnsureSuccessStatusCode();
        // qBittorrent does not return the hash. Match the added torrent by its unique name after a short sync window.
        string? hash = null;
        for (var attempt = 0; attempt < 8 && hash is null; attempt++)
        {
            await Task.Delay(350, ct); using var data = await MainDataAsync(0, ct);
            if (data.RootElement.TryGetProperty("torrents", out var torrents))
                hash = torrents.EnumerateObject().FirstOrDefault(p => string.Equals(p.Value.GetProperty("name").GetString(), torrentName, StringComparison.Ordinal)).Name;
        }
        if (hash is null) throw new InvalidOperationException(LocalizationService.Instance["TorrentAcceptedUnidentified"]);
        var excluded = files.Select((f, i) => (f, i)).Where(x => !x.f.IsSelected).Select(x => x.i.ToString()).ToArray();
        if (excluded.Length > 0) await PostFormAsync("/api/v2/torrents/filePrio", new() { ["hash"] = hash, ["id"] = string.Join('|', excluded), ["priority"] = "0" }, ct);
        if (!paused) await PostFormAsync("/api/v2/torrents/resume", new() { ["hashes"] = hash }, ct);
    }
    public Task PauseAsync(string hash, CancellationToken ct) => PostFormAsync("/api/v2/torrents/pause", new() { ["hashes"] = hash }, ct);
    public Task ResumeAsync(string hash, CancellationToken ct) => PostFormAsync("/api/v2/torrents/resume", new() { ["hashes"] = hash }, ct);
    public Task DeleteAsync(string hash, bool deleteFiles, CancellationToken ct) => PostFormAsync("/api/v2/torrents/delete", new() { ["hashes"] = hash, ["deleteFiles"] = deleteFiles ? "true" : "false" }, ct);
    public Task RecheckAsync(string hash, CancellationToken ct) => PostFormAsync("/api/v2/torrents/recheck", new() { ["hashes"] = hash }, ct);
    public Task ReannounceAsync(string hash, CancellationToken ct) => PostFormAsync("/api/v2/torrents/reannounce", new() { ["hashes"] = hash }, ct);
    public Task ForceStartAsync(string hash, bool enabled, CancellationToken ct) => PostFormAsync("/api/v2/torrents/setForceStart", new() { ["hashes"] = hash, ["value"] = enabled ? "true" : "false" }, ct);
    public Task FilePriorityAsync(string hash, string ids, int priority, CancellationToken ct) => PostFormAsync("/api/v2/torrents/filePrio", new() { ["hash"] = hash, ["id"] = ids, ["priority"] = priority.ToString() }, ct);
    public Task SetLocationAsync(string hash, string location, CancellationToken ct) => PostFormAsync("/api/v2/torrents/setLocation", new() { ["hashes"] = hash, ["location"] = location }, ct);
    public Task SetCategoryAsync(string hash, string category, CancellationToken ct) => PostFormAsync("/api/v2/torrents/setCategory", new() { ["hashes"] = hash, ["category"] = category }, ct);
    public Task AddTagsAsync(string hash, string tags, CancellationToken ct) => PostFormAsync("/api/v2/torrents/addTags", new() { ["hashes"] = hash, ["tags"] = tags }, ct);
    public Task RemoveTagsAsync(string hash, string tags, CancellationToken ct) => PostFormAsync("/api/v2/torrents/removeTags", new() { ["hashes"] = hash, ["tags"] = tags }, ct);
    public Task CreateCategoryAsync(string category, string savePath, CancellationToken ct) => PostFormAsync("/api/v2/torrents/createCategory", new() { ["category"] = category, ["savePath"] = savePath }, ct);
    public Task EditCategoryAsync(string category, string savePath, CancellationToken ct) => PostFormAsync("/api/v2/torrents/editCategory", new() { ["category"] = category, ["savePath"] = savePath }, ct);
    public Task DeleteCategoryAsync(string category, CancellationToken ct) => PostFormAsync("/api/v2/torrents/removeCategories", new() { ["categories"] = category }, ct);
    public async Task<JsonDocument> GetTorrentDataAsync(string endpoint, string hash, CancellationToken ct)
    {
        using var response = await SendAsync(() => _http.GetAsync($"{_baseUrl}{endpoint}?hash={Uri.EscapeDataString(hash)}", ct), ct);
        response.EnsureSuccessStatusCode(); return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    private async Task PostFormAsync(string endpoint, Dictionary<string, string> values, CancellationToken ct)
    { using var response = await SendAsync(() => _http.PostAsync(_baseUrl + endpoint, new FormUrlEncodedContent(values), ct), ct); response.EnsureSuccessStatusCode(); }
    public void Dispose() => _http.Dispose();
}
