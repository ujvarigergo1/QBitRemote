using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace QBitRemote.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow _window;
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _iconImage;
    private readonly Forms.ToolStripMenuItem _showItem;
    private readonly Forms.ToolStripMenuItem _exitItem;

    public TrayIconService(MainWindow window)
    {
        _window = window;
        _iconImage = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application;
        var menu = new Forms.ContextMenuStrip();
        _showItem = new Forms.ToolStripMenuItem(); _showItem.Click += (_, _) => RestoreWindow(); menu.Items.Add(_showItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        _exitItem = new Forms.ToolStripMenuItem(); _exitItem.Click += (_, _) => ExitApplication(); menu.Items.Add(_exitItem);
        _icon = new Forms.NotifyIcon { Icon = _iconImage, Text = "QBitRemote", Visible = true, ContextMenuStrip = menu };
        RefreshText();
        _icon.DoubleClick += (_, _) => RestoreWindow();
    }

    public void HideWindow() => _window.Hide();

    public void RefreshText()
    {
        var l = LocalizationService.Instance;
        _showItem.Text = l["ShowApplication"];
        _exitItem.Text = l["Exit"];
    }

    public void ShowNotification(string title, string message) => _icon.ShowBalloonTip(4000, title, message, Forms.ToolTipIcon.Info);

    public void RestoreWindow()
    {
        if (!_window.IsVisible) _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
        _window.Focus();
    }

    private void ExitApplication()
    {
        _window.AllowClose();
        _window.Close();
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        if (!ReferenceEquals(_iconImage, SystemIcons.Application)) _iconImage.Dispose();
    }
}
