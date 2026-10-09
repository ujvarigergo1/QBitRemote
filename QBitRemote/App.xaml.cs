using System.IO.Pipes;
using System.Text;
using System.Windows;
using QBitRemote.Services;
using QBitRemote.ViewModels;

namespace QBitRemote;
public partial class App : System.Windows.Application
{
    private const string MutexName = "QBitRemote.Singleton.8D54BDE5"; private const string PipeName = "QBitRemote.OpenTorrent.8D54BDE5";
    private Mutex? _mutex; private bool _ownsMutex; private CancellationTokenSource? _pipeStop; private MainWindow? _main; private TrayIconService? _tray;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); _mutex = new Mutex(true, MutexName, out var first); _ownsMutex = first;
        var torrent = e.Args.FirstOrDefault(a => a.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        if (!first) { if (torrent is not null) await SendPathAsync(torrent); Shutdown(); return; }
        var viewModel = new MainViewModel(); ThemeService.Apply(viewModel.Settings.Theme); _main = new MainWindow(viewModel); _tray = new TrayIconService(_main); MainWindow = _main; _main.Show(); _pipeStop = new(); _ = ListenAsync(_pipeStop.Token); var connected = await viewModel.StartAsync(); if (!connected) new ConnectionWindow(viewModel) { Owner = _main }.ShowDialog(); if (torrent is not null) _main.OpenTorrent(torrent);
    }
    private static async Task SendPathAsync(string path)
    { try { using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous); await pipe.ConnectAsync(1500); var bytes = Encoding.UTF8.GetBytes(path); await pipe.WriteAsync(bytes); } catch (Exception ex) { LogService.Write("Could not send torrent to existing instance", ex); } }
    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested) try { using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous); await pipe.WaitForConnectionAsync(ct); using var reader = new StreamReader(pipe, Encoding.UTF8); var path = await reader.ReadToEndAsync(ct); if (File.Exists(path)) await Dispatcher.InvokeAsync(() => { _main?.Activate(); _main?.OpenTorrent(path); }); } catch (OperationCanceledException) { break; } catch (Exception ex) { LogService.Write("Pipe listener error", ex); }
    }
    public void ShowNotification(string title, string message, bool sound) { _tray?.ShowNotification(title, message); if (sound) System.Media.SystemSounds.Asterisk.Play(); LogService.Write($"{title}: {message}"); }
    public void HideToTray() => _tray?.HideWindow();
    public void RefreshTrayText() => _tray?.RefreshText();
    protected override void OnExit(ExitEventArgs e) { _tray?.Dispose(); _pipeStop?.Cancel(); if (_ownsMutex) _mutex?.ReleaseMutex(); _mutex?.Dispose(); base.OnExit(e); }
}
