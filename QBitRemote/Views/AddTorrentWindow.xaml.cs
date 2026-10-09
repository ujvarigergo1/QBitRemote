using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using QBitRemote.Services;
using QBitRemote.ViewModels;

namespace QBitRemote.Views;
public partial class AddTorrentWindow : Window
{
    private readonly MainViewModel _main;
    private readonly ObservableCollection<AddTorrentViewModel> _torrents = [];
    private AddTorrentViewModel _vm = null!;

    public AddTorrentWindow(MainViewModel main, IEnumerable<string> paths)
    {
        InitializeComponent(); _main = main; AddPaths(paths); Loaded += OnLoaded;
    }
    public AddTorrentWindow(MainViewModel main, string path) : this(main, [path]) { }

    public void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(File.Exists))
        {
            if (_torrents.Any(torrent => string.Equals(torrent.FilePath, path, StringComparison.OrdinalIgnoreCase))) continue;
            _torrents.Add(new AddTorrentViewModel(path, TorrentMetadataReader.Read(path), _main.Settings));
        }
        TorrentSelector.ItemsSource = _torrents;
        if (_torrents.Count > 0 && TorrentSelector.SelectedItem is null) TorrentSelector.SelectedIndex = 0;
        if (TorrentSelector.SelectedItem is AddTorrentViewModel selected) { _vm = selected; DataContext = _vm; }
        TorrentSwitchPanel.Visibility = _torrents.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var (categories, tags) = await _main.LabelsAsync();
            foreach (var torrent in _torrents)
            {
                torrent.SetServerCategories(categories);
                foreach (var tag in tags) if (!torrent.Tags.Any(existing => existing.Name.Equals(tag, StringComparison.OrdinalIgnoreCase))) torrent.Tags.Add(new TagOption(tag));
            }
        }
        catch (Exception ex) { MessageBox.Show(this, string.Format(LocalizationService.Instance["LoadCategoriesFailed"], ex.Message), "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void TorrentSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TorrentSelector.SelectedItem is AddTorrentViewModel torrent) { _vm = torrent; DataContext = _vm; }
    }
    private void ApplyToAll_Click(object sender, RoutedEventArgs e)
    {
        var selectedTags = _vm.Tags.Where(tag => tag.IsSelected).Select(tag => tag.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var torrent in _torrents.Where(torrent => !ReferenceEquals(torrent, _vm)))
        {
            torrent.SavePath = _vm.SavePath; torrent.Category = _vm.Category; torrent.Paused = _vm.Paused;
            foreach (var tag in torrent.Tags) tag.IsSelected = selectedTags.Contains(tag.Name);
        }
    }
    private void SelectAll_Click(object sender, RoutedEventArgs e) => _vm.SelectAll(true);
    private void SelectNone_Click(object sender, RoutedEventArgs e) => _vm.SelectAll(false);
    private void Category_SelectionChanged(object sender, SelectionChangedEventArgs e) => _vm.ApplyCategoryPath();
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var l = LocalizationService.Instance;
        var duplicates = _torrents.Where(candidate => _main.Torrents.Any(torrent => torrent.Name.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase))).ToList();
        if (_main.Settings.WarnOnDuplicate && duplicates.Count > 0 && MessageBox.Show(this, string.Format(l["DuplicateTorrentText"], duplicates[0].Name), l["DuplicateTorrentWarning"], MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            IsEnabled = false;
            foreach (var torrent in _torrents) await _main.AddAsync(torrent.FilePath, torrent.Name, torrent.SavePath, torrent.Category, torrent.Tags.Where(tag => tag.IsSelected).Select(tag => tag.Name), torrent.Paused, torrent.Files);
            DialogResult = true;
        }
        catch (Exception ex) { IsEnabled = true; MessageBox.Show(this, string.Format(l["AddTorrentFailed"], ex.Message), "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
