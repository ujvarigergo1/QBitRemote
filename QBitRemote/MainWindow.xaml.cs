using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using QBitRemote.Models;
using QBitRemote.ViewModels;
using QBitRemote.Views;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using ListBox = System.Windows.Controls.ListBox;
using Panel = System.Windows.Controls.Panel;
using Control = System.Windows.Controls.Control;

namespace QBitRemote;
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _allowClose;
    private HashSet<string> _selectedTorrentHashes = [];
    public static IValueConverter SpeedConverter { get; } = new SpeedFormatter();
    public static IValueConverter SizeConverter { get; } = new SizeFormatter();
    public MainWindow(MainViewModel vm) { InitializeComponent(); _vm = vm; RestoreWindowPlacement(); DataContext = vm; _vm.Refreshing += RememberSelection; _vm.TorrentsUpdated += RestoreSelection; Closing += MainWindow_Closing; StateChanged += MainWindow_StateChanged; Closed += (_, _) => { SaveWindowPlacement(); _vm.Refreshing -= RememberSelection; _vm.TorrentsUpdated -= RestoreSelection; vm.Dispose(); }; }
    public void AllowClose() => _allowClose = true;
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveWindowPlacement();
        if (_allowClose || !_vm.Settings.CloseToTray) return;
        e.Cancel = true;
        ((App)Application.Current).HideToTray();
    }
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _vm.Settings.MinimizeToTray) ((App)Application.Current).HideToTray();
    }
    private void RestoreWindowPlacement()
    {
        var settings = _vm.Settings;
        if (settings.MainWindowLeft is not double left || settings.MainWindowTop is not double top || settings.MainWindowWidth is not double width || settings.MainWindowHeight is not double height) return;
        if (width < MinWidth || height < MinHeight || width > SystemParameters.VirtualScreenWidth || height > SystemParameters.VirtualScreenHeight) return;
        var virtualScreen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var savedBounds = new Rect(left, top, width, height);
        if (!virtualScreen.IntersectsWith(savedBounds)) return;
        Left = left; Top = top; Width = width; Height = height;
        if (settings.MainWindowMaximized) WindowState = WindowState.Maximized;
    }
    private void SaveWindowPlacement()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (bounds.Width < MinWidth || bounds.Height < MinHeight) return;
        var settings = _vm.Settings;
        settings.MainWindowLeft = bounds.Left; settings.MainWindowTop = bounds.Top; settings.MainWindowWidth = bounds.Width; settings.MainWindowHeight = bounds.Height; settings.MainWindowMaximized = WindowState == WindowState.Maximized;
        try { new Services.SettingsService().Save(settings); } catch { }
    }
    private void RememberSelection() => _selectedTorrentHashes = TorrentGrid.SelectedItems.OfType<TorrentInfo>().Select(torrent => torrent.Hash).ToHashSet(StringComparer.OrdinalIgnoreCase);
    private void RestoreSelection()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_selectedTorrentHashes.Count == 0) return;
            TorrentGrid.SelectedItems.Clear();
            foreach (var torrent in _vm.Torrents.Where(torrent => _selectedTorrentHashes.Contains(torrent.Hash) && TorrentGrid.Items.Contains(torrent))) TorrentGrid.SelectedItems.Add(torrent);
        });
    }
    public void OpenTorrent(string path)
    {
        OpenTorrentFiles([path]);
    }
    private void AddTorrent_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = _vm.L["BrowseTorrents"], Filter = _vm.L["TorrentFileFilter"], Multiselect = true, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) OpenTorrentFiles(dialog.FileNames);
    }
    private void OpenTorrentFiles(IEnumerable<string> paths)
    {
        var selectedPaths = paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (selectedPaths.Count == 0) return;
        try
        {
            var existing = Application.Current.Windows.OfType<AddTorrentWindow>().FirstOrDefault();
            if (existing is not null) { existing.AddPaths(selectedPaths); existing.Activate(); return; }
            new AddTorrentWindow(_vm, selectedPaths) { Owner = this }.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show(this, string.Format(_vm.L["ReadTorrentFailed"], ex.Message), "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private async void Pause_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is TorrentInfo t) await RunAction(() => _vm.PauseAsync(t)); }
    private async void Resume_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is TorrentInfo t) await RunAction(() => _vm.ResumeAsync(t)); }
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not TorrentInfo t) return;
        var dialog = new DeleteConfirmWindow { Owner = this }; if (dialog.ShowDialog() == true) await RunAction(() => _vm.DeleteAsync(t, dialog.DeleteFiles));
    }
    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is TorrentInfo torrent) new TorrentDetailsWindow(_vm, torrent) { Owner = this }.ShowDialog();
    }
    private void TorrentGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (TorrentGrid.SelectedItem is TorrentInfo torrent) new TorrentDetailsWindow(_vm, torrent) { Owner = this }.ShowDialog();
    }
    private void TorrentGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.Item is TorrentInfo && !TorrentGrid.SelectedItems.Contains(row.Item)) { TorrentGrid.SelectedItems.Clear(); row.IsSelected = true; row.Focus(); }
    }
    private void TorrentGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        var direction = e.Column.SortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        foreach (var column in TorrentGrid.Columns) { column.SortDirection = null; if (column.Header is string text) column.Header = text.TrimEnd(' ', '▲', '▼'); }
        e.Column.SortDirection = direction;
        e.Column.Header = $"{e.Column.Header} {(direction == ListSortDirection.Ascending ? '▲' : '▼')}";
        var property = e.Column.SortMemberPath; if (string.IsNullOrWhiteSpace(property) && e.Column is DataGridBoundColumn bound && bound.Binding is Binding binding) property = binding.Path?.Path;
        if (string.IsNullOrWhiteSpace(property)) return;
        _vm.ApplyManualSort(property, direction);
    }
    private async void ContextAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string action }) return;
        var torrents = TorrentGrid.SelectedItems.OfType<TorrentInfo>().ToList();
        if (torrents.Count == 0) return;
        try
        {
            switch (action)
            {
                case "details": new TorrentDetailsWindow(_vm, torrents[0]) { Owner = this }.ShowDialog(); break;
                case "pause": await RunSelectedAction(torrents, _vm.PauseAsync); break;
                case "resume": await RunSelectedAction(torrents, _vm.ResumeAsync); break;
                case "recheck": await RunSelectedAction(torrents, _vm.RecheckAsync); break;
                case "location": { var value = InputWindow.Ask(this, _vm.L["ChangeDownloadLocation"], _vm.L["ServerSidePath"], ""); if (!string.IsNullOrWhiteSpace(value)) await RunSelectedAction(torrents, torrent => _vm.SetLocationAsync(torrent, value)); break; }
                case "category": { var value = InputWindow.Ask(this, _vm.L["ChangeCategory"], _vm.L["ChangeCategoryPrompt"], torrents[0].Category); if (value is not null) await RunSelectedAction(torrents, torrent => _vm.SetCategoryAsync(torrent, value)); break; }
                case "addtags": { var (_, tags) = await _vm.LabelsAsync(); var selected = TagPickerWindow.Pick(this, _vm.L["AddTagsTitle"], tags); if (selected.Count > 0) await RunSelectedAction(torrents, torrent => _vm.AddTagsAsync(torrent, string.Join(',', selected))); break; }
                case "removetags": { var sharedTags = torrents.Select(torrent => torrent.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase)).Aggregate((left, right) => { left.IntersectWith(right); return left; }); var selected = TagPickerWindow.Pick(this, _vm.L["RemoveTagsTitle"], sharedTags); if (selected.Count > 0) await RunSelectedAction(torrents, torrent => _vm.RemoveTagsAsync(torrent, string.Join(',', selected))); break; }
                case "delete": { var dialog = new DeleteConfirmWindow { Owner = this }; if (dialog.ShowDialog() == true) await RunSelectedAction(torrents, torrent => _vm.DeleteAsync(torrent, dialog.DeleteFiles)); break; }
            }
            await _vm.RefreshAsync();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private static async Task RunSelectedAction(IEnumerable<TorrentInfo> torrents, Func<TorrentInfo, Task> action) { foreach (var torrent in torrents) await action(torrent); }
    private async Task RunAction(Func<Task> action) { try { await action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private void Settings_Click(object sender, RoutedEventArgs e) { var w = new SettingsWindow(_vm) { Owner = this }; w.ShowDialog(); }
    private void Connection_Click(object sender, RoutedEventArgs e) => new ConnectionWindow(_vm) { Owner = this }.ShowDialog();
    private sealed class SpeedFormatter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is long v ? MainViewModel.FormatSpeed(v) : ""; public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing; }
    private sealed class SizeFormatter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is long v ? MainViewModel.FormatSize(v) : ""; public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing; }
    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject { while (current is not null) { if (current is T typed) return typed; current = VisualTreeHelper.GetParent(current); } return null; }
}

public sealed class InputWindow : Window
{
    private readonly TextBox _value;
    private InputWindow(string title, string label, string value)
    {
        Title = title; Width = 460; Height = 175; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; FontFamily = new FontFamily("Segoe UI");
        _value = new TextBox { Text = value, Margin = new Thickness(0, 6, 0, 12), MinWidth = 360 }; var labelBlock = new TextBlock { Text = label };
        var l = Services.LocalizationService.Instance; var ok = new Button { Content = l["Save"], IsDefault = true, MinWidth = 88 }; ok.Click += (_, _) => DialogResult = true; var cancel = new Button { Content = l["Cancel"], IsCancel = true, MinWidth = 88 }; var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; buttons.Children.Add(cancel); buttons.Children.Add(ok);
        var panel = new StackPanel { Margin = new Thickness(20) }; panel.Children.Add(labelBlock); panel.Children.Add(_value); panel.Children.Add(buttons); Content = panel; Loaded += (_, _) => { _value.Focus(); _value.SelectAll(); };
    }
    public static string? Ask(Window owner, string title, string label, string value) { var dialog = new InputWindow(title, label, value) { Owner = owner }; return dialog.ShowDialog() == true ? dialog._value.Text.Trim() : null; }
}

public sealed class TagPickerWindow : Window
{
    private readonly ListBox _list = new() { SelectionMode = SelectionMode.Multiple, MinHeight = 220 };
    private TagPickerWindow(string title, IEnumerable<string> tags)
    {
        Title = title; Width = 390; Height = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI");
        foreach (var tag in tags.Order()) _list.Items.Add(tag);
        var l = Services.LocalizationService.Instance; var ok = new Button { Content = l["Apply"], IsDefault = true, MinWidth = 88 }; ok.Click += (_, _) => DialogResult = true; var cancel = new Button { Content = l["Cancel"], IsCancel = true, MinWidth = 88 }; var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; buttons.Children.Add(cancel); buttons.Children.Add(ok);
        var panel = new DockPanel { Margin = new Thickness(18) }; DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons); panel.Children.Add(_list); Content = panel;
    }
    public static List<string> Pick(Window owner, string title, IEnumerable<string> tags) { var dialog = new TagPickerWindow(title, tags) { Owner = owner }; return dialog.ShowDialog() == true ? dialog._list.SelectedItems.Cast<string>().ToList() : []; }
}

public sealed class DeleteConfirmWindow : Window
{
    public bool DeleteFiles { get; private set; }
    public DeleteConfirmWindow()
    {
        var l = Services.LocalizationService.Instance; Title = l["ConfirmDeleteTorrent"]; Width = 390; Height = 170; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var check = new CheckBox { Content = l["DeleteDownloadedFiles"], IsChecked = true, Margin = new Thickness(14) }; var ok = new Button { Content = l["ConfirmDeleteTorrent"], IsDefault = true, Margin = new Thickness(4), MinWidth = 120 }; ok.Click += (_, _) => { DeleteFiles = check.IsChecked == true; DialogResult = true; };
        var cancel = new Button { Content = l["Cancel"], IsCancel = true, Margin = new Thickness(4), MinWidth = 88 }; var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; buttons.Children.Add(cancel); buttons.Children.Add(ok);
        var panel = new DockPanel { Margin = new Thickness(12) }; DockPanel.SetDock(check, Dock.Top); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(check); panel.Children.Add(buttons); Content = panel;
    }
}

public sealed class SettingsWindow : Window
{
    private readonly MainViewModel _vm; private readonly ComboBox _language = new() { Margin = new Thickness(0, 3, 0, 12) }; private readonly CheckBox _association = new() { Margin = new Thickness(0, 8, 0, 2) }, _startup = new() { Content = "Start with Windows" }, _minimize = new() { Content = "Minimize to system tray" }, _close = new() { Content = "Close to system tray" }, _notify = new() { Content = "Notify when a torrent completes" }, _sound = new() { Content = "Play completion sound" }, _paused = new() { Content = "Add torrents paused by default" }, _duplicates = new() { Content = "Warn if torrent may already exist" };
    private readonly ComboBox _refresh = new() { ItemsSource = new[] { 1, 2, 5, 10, 30, 60 } }, _theme = new() { ItemsSource = Services.ThemeService.Options }; private readonly TextBox _path = new(), _category = new(), _tags = new();
    public SettingsWindow(MainViewModel vm)
    {
        _vm = vm; var l = vm.L; Title = l["Settings"]; Width = 540; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        _language.ItemsSource = l.Languages; _language.SelectedItem = l.Languages.FirstOrDefault(x => x.Code == (vm.Settings.Language ?? l.Language));
        _association.Content = l["OpenAssociation"]; _startup.Content = l["StartWithWindows"]; _minimize.Content = l["MinimizeToTray"]; _close.Content = l["CloseToTray"]; _notify.Content = l["NotifyCompletion"]; _sound.Content = l["PlaySound"]; _paused.Content = l["AddPausedDefault"]; _duplicates.Content = l["WarnDuplicate"];
        _theme.SelectedItem = Services.ThemeService.Options.FirstOrDefault(option => option.Value == vm.Settings.Theme); _association.IsChecked = Services.FileAssociationService.IsRegistered();
        _startup.IsChecked = vm.Settings.StartWithWindows; _minimize.IsChecked = vm.Settings.MinimizeToTray; _close.IsChecked = vm.Settings.CloseToTray; _notify.IsChecked = vm.Settings.NotifyOnCompletion; _sound.IsChecked = vm.Settings.PlayCompletionSound; _refresh.SelectedItem = vm.Settings.RefreshIntervalSeconds; _path.Text = vm.Settings.DefaultSavePath; _category.Text = vm.Settings.DefaultCategory; _tags.Text = vm.Settings.DefaultTags; _paused.IsChecked = vm.Settings.AddPausedByDefault; _duplicates.IsChecked = vm.Settings.WarnOnDuplicate;
        var tabs = new TabControl { Margin = new Thickness(18) };
        var general = new StackPanel { Margin = new Thickness(14) }; general.Children.Add(new TextBlock { Text = l["Language"] }); general.Children.Add(_language); AddField(general, l["Theme"], _theme); general.Children.Add(_association); general.Children.Add(new TextBlock { Text = l["AssociationHint"], Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }); general.Children.Add(_startup); general.Children.Add(_minimize); general.Children.Add(_close); tabs.Items.Add(new TabItem { Header = l["General"], Content = general });
        var downloads = new StackPanel { Margin = new Thickness(14) }; AddField(downloads, l["RefreshInterval"], _refresh); AddField(downloads, l["DefaultSavePath"], _path); AddField(downloads, l["DefaultCategory"], _category); AddField(downloads, l["DefaultTags"], _tags); downloads.Children.Add(_paused); downloads.Children.Add(_duplicates); var categories = new Button { Content = l["ManageServerCategories"], HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0) }; categories.Click += (_, _) => new CategoryManagerWindow(_vm) { Owner = this }.ShowDialog(); downloads.Children.Add(categories); tabs.Items.Add(new TabItem { Header = l["TorrentDefaults"], Content = new ScrollViewer { Content = downloads } });
        var notifications = new StackPanel { Margin = new Thickness(14) }; notifications.Children.Add(_notify); notifications.Children.Add(_sound); tabs.Items.Add(new TabItem { Header = l["Notifications"], Content = notifications });
        var host = new DockPanel(); var save = new Button { Content = l["Save"], IsDefault = true }; save.Click += Save_Click; var cancel = new Button { Content = l["Cancel"], IsCancel = true }; var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 18, 14) }; buttons.Children.Add(cancel); buttons.Children.Add(save); DockPanel.SetDock(buttons, Dock.Bottom); host.Children.Add(buttons); host.Children.Add(tabs); Content = host;
    }
    private static void AddField(Panel panel, string label, Control control) { panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 5, 0, 1) }); panel.Children.Add(control); }
    private void Save_Click(object sender, RoutedEventArgs e) { try { _vm.SaveLanguage((_language.SelectedItem as Services.LanguageOption)?.Code); ((App)Application.Current).RefreshTrayText(); var s = _vm.Settings; s.Theme = (_theme.SelectedItem as Services.ThemeOption)?.Value ?? "system"; Services.ThemeService.Apply(s.Theme); s.StartWithWindows = _startup.IsChecked == true; s.MinimizeToTray = _minimize.IsChecked == true; s.CloseToTray = _close.IsChecked == true; s.NotifyOnCompletion = _notify.IsChecked == true; s.PlayCompletionSound = _sound.IsChecked == true; s.RefreshIntervalSeconds = _refresh.SelectedItem is int seconds ? seconds : 1; s.DefaultSavePath = _path.Text.Trim(); s.DefaultCategory = _category.Text.Trim(); s.DefaultTags = _tags.Text.Trim(); s.AddPausedByDefault = _paused.IsChecked == true; s.WarnOnDuplicate = _duplicates.IsChecked == true; Services.StartupService.SetEnabled(s.StartWithWindows); new Services.SettingsService().Save(s); if (_association.IsChecked == true) Services.FileAssociationService.Register(); else Services.FileAssociationService.Unregister(); DialogResult = true; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Error); } }
}

public sealed class ConnectionWindow : Window
{
    private readonly MainViewModel _vm; private readonly TextBox _url = new(), _user = new(); private readonly PasswordBox _password = new(); private readonly ComboBox _profiles = new();
    public ConnectionWindow(MainViewModel vm)
    {
        _vm = vm; var l = vm.L; Title = l["Connection"]; Width = 480; Height = 270; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; FontFamily = new FontFamily("Segoe UI"); _url.Text = vm.Settings.ServerUrl; _user.Text = vm.Settings.Username; _password.Password = Services.SettingsService.Unprotect(vm.Settings.ProtectedPassword); _profiles.ItemsSource = vm.Settings.ServerProfiles; _profiles.SelectionChanged += ProfileChanged;
        var currentUrl = vm.Settings.ServerUrl.Trim().TrimEnd('/');
        _profiles.SelectedItem = vm.Settings.ServerProfiles.FirstOrDefault(profile => profile.ServerUrl.Trim().TrimEnd('/').Equals(currentUrl, StringComparison.OrdinalIgnoreCase) && profile.Username.Equals(vm.Settings.Username, StringComparison.OrdinalIgnoreCase));
        var grid = new Grid { Margin = new Thickness(20) }; for (var i = 0; i < 5; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); Add(grid, l["Preset"], _profiles, 0); Add(grid, l["ServerUrl"], _url, 1); Add(grid, l["Username"], _user, 2); Add(grid, l["Password"], _password, 3); var save = new Button { Content = l["SaveConnect"], IsDefault = true }; save.Click += Save_Click; var cancel = new Button { Content = l["Cancel"], IsCancel = true }; var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; buttons.Children.Add(cancel); buttons.Children.Add(save); Grid.SetRow(buttons, 4); Grid.SetColumn(buttons, 1); grid.Children.Add(buttons); Content = grid;
    }
    private static void Add(Grid grid, string label, Control control, int row) { var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(text, row); grid.Children.Add(text); Grid.SetRow(control, row); Grid.SetColumn(control, 1); grid.Children.Add(control); }
    private void ProfileChanged(object sender, SelectionChangedEventArgs e) { if (_profiles.SelectedItem is ServerProfile p) { _url.Text = p.ServerUrl; _user.Text = p.Username; _password.Password = Services.SettingsService.Unprotect(p.ProtectedPassword); } }
    private async void Save_Click(object sender, RoutedEventArgs e) { try { IsEnabled = false; await _vm.SaveSettingsAsync(_url.Text, _user.Text, _password.Password); DialogResult = true; } catch (Exception ex) { IsEnabled = true; MessageBox.Show(this, ex.Message, "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Error); } }
}

public sealed class CategoryManagerWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly ListBox _categories = new() { MinHeight = 260, DisplayMemberPath = nameof(CategoryEntry.Display) };
    public CategoryManagerWindow(MainViewModel vm)
    {
        _vm = vm; var l = vm.L; Title = l["ServerCategories"]; Width = 680; Height = 450; WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI");
        var add = new Button { Content = l["Add"], MinWidth = 78 }; add.Click += Add_Click;
        var edit = new Button { Content = l["Edit"], MinWidth = 78, Margin = new Thickness(6, 0, 0, 0) }; edit.Click += Edit_Click;
        var delete = new Button { Content = l["Delete"], MinWidth = 78, Margin = new Thickness(6, 0, 0, 0) }; delete.Click += Delete_Click;
        var refresh = new Button { Content = l["Refresh"], MinWidth = 78, Margin = new Thickness(6, 0, 0, 0) }; refresh.Click += async (_, _) => await LoadAsync();
        var close = new Button { Content = l["Close"], IsCancel = true, MinWidth = 88 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) }; actions.Children.Add(add); actions.Children.Add(edit); actions.Children.Add(delete); actions.Children.Add(refresh);
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) }; bottom.Children.Add(close);
        var note = new TextBlock { Text = l["ServerCategoryNote"], Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        var panel = new DockPanel { Margin = new Thickness(18) }; DockPanel.SetDock(actions, Dock.Top); DockPanel.SetDock(note, Dock.Top); DockPanel.SetDock(bottom, Dock.Bottom); panel.Children.Add(actions); panel.Children.Add(note); panel.Children.Add(bottom); panel.Children.Add(_categories); Content = panel;
        Loaded += async (_, _) => await LoadAsync();
    }
    private async Task LoadAsync()
    {
        try { IsEnabled = false; var (paths, _) = await _vm.LabelsAsync(); _categories.ItemsSource = paths.OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase).Select(x => new CategoryEntry(x.Key, x.Value)).ToList(); }
        catch (Exception ex) { MessageBox.Show(this, string.Format(Services.LocalizationService.Instance["LoadServerCategoriesFailed"], ex.Message), "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsEnabled = true; }
    }
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CategoryEditorWindow(); dialog.Owner = this;
        if (dialog.ShowDialog() != true) return;
        try { await _vm.CreateCategoryAsync(dialog.CategoryName, dialog.SavePath); await LoadAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_categories.SelectedItem is not CategoryEntry item) return;
        var dialog = new CategoryEditorWindow(item.Name, item.Path, true) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try { await _vm.EditCategoryAsync(item.Name, dialog.SavePath); await LoadAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_categories.SelectedItem is not CategoryEntry item) return;
        var l = Services.LocalizationService.Instance;
        if (MessageBox.Show(this, string.Format(l["DeleteCategoryConfirm"], item.Name), l["DeleteCategoryTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { await _vm.DeleteCategoryAsync(item.Name); await LoadAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private sealed record CategoryEntry(string Name, string Path) { public string Display => $"{Name}    —    {Path}"; }
}

public sealed class CategoryEditorWindow : Window
{
    private readonly TextBox _name = new(), _path = new();
    public string CategoryName => _name.Text.Trim();
    public string SavePath => _path.Text.Trim();
    public CategoryEditorWindow(string name = "", string path = "", bool nameLocked = false)
    {
        var l = Services.LocalizationService.Instance; Title = nameLocked ? l["EditCategory"] : l["AddCategory"]; Width = 500; Height = 205; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI");
        _name.Text = name; _name.IsReadOnly = nameLocked; _path.Text = path;
        var grid = new Grid { Margin = new Thickness(20) }; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); for (var i = 0; i < 3; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Add(grid, l["Category"], _name, 0); Add(grid, l["SavePath"], _path, 1);
        var ok = new Button { Content = l["Save"], IsDefault = true, MinWidth = 88 }; ok.Click += (_, _) => { if (CategoryName.Length == 0) { MessageBox.Show(this, l["CategoryRequired"], "QBitRemote", MessageBoxButton.OK, MessageBoxImage.Warning); return; } DialogResult = true; };
        var cancel = new Button { Content = l["Cancel"], IsCancel = true, MinWidth = 88 }; var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; buttons.Children.Add(cancel); buttons.Children.Add(ok); Grid.SetRow(buttons, 2); Grid.SetColumn(buttons, 1); grid.Children.Add(buttons); Content = grid;
    }
    private static void Add(Grid grid, string label, Control control, int row) { var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(text, row); grid.Children.Add(text); Grid.SetRow(control, row); Grid.SetColumn(control, 1); grid.Children.Add(control); }
}
