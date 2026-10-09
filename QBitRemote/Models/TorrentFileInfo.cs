using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QBitRemote.Models;

public sealed class TorrentFileInfo : INotifyPropertyChanged
{
    private bool _isSelected = true;
    public string Path { get; init; } = "";
    public long Size { get; init; }
    public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
