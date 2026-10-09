using System.Collections.ObjectModel;
using QBitRemote.Infrastructure;
using QBitRemote.Models;
using QBitRemote.Services;

namespace QBitRemote.ViewModels;
public sealed class AddTorrentViewModel : ObservableObject
{
    public string FilePath { get; } public string Name { get; } public string Size => MainViewModel.FormatSize(Files.Sum(f => f.Size));
    public ObservableCollection<TorrentFileInfo> Files { get; } public ObservableCollection<string> Paths { get; } public ObservableCollection<string> Categories { get; } = [""];
    public ObservableCollection<TagOption> Tags { get; } = [];
    private string _savePath = "", _category = ""; private bool _paused;
    public string SavePath { get => _savePath; set => Set(ref _savePath, value); } public string Category { get => _category; set => Set(ref _category, value); } public bool Paused { get => _paused; set => Set(ref _paused, value); }
    private readonly AppSettings _settings; private readonly Dictionary<string, string> _serverCategoryPaths = new(StringComparer.OrdinalIgnoreCase);
    public AddTorrentViewModel(string path, TorrentMetadata metadata, AppSettings settings)
    { FilePath = path; Name = metadata.Name; Files = new(metadata.Files); _settings = settings; Paths = new(settings.RecentSavePaths); SavePath = settings.DefaultSavePath.Length > 0 ? settings.DefaultSavePath : settings.RecentSavePaths.FirstOrDefault() ?? ""; Category = settings.DefaultCategory; Paused = settings.AddPausedByDefault; foreach (var tag in settings.DefaultTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) Tags.Add(new TagOption(tag) { IsSelected = true }); }
    public void SetServerCategories(Dictionary<string, string> paths) { _serverCategoryPaths.Clear(); foreach (var (category, path) in paths) { _serverCategoryPaths[category] = path; Categories.Add(category); } ApplyCategoryPath(); }
    public void ApplyCategoryPath() { if (!string.IsNullOrWhiteSpace(Category) && _serverCategoryPaths.TryGetValue(Category, out var path) && !string.IsNullOrWhiteSpace(path)) SavePath = path; }
    public void SelectAll(bool selected) { foreach (var f in Files) f.IsSelected = selected; }
}
public sealed class TagOption(string name) : ObservableObject { private bool _selected; public string Name { get; } = name; public bool IsSelected { get => _selected; set => Set(ref _selected, value); } }
