namespace QBitRemote.Models;

using QBitRemote.Services;

public sealed record TorrentInfo
{
    public string Hash { get; init; } = "";
    public string Name { get; init; } = "";
    public double Progress { get; init; }
    public string Status { get; init; } = "";
    public string StatusText => TorrentStatusService.GetLabel(Status);
    public string StatusIcon => TorrentStatusService.GetIcon(Status);
    public long DownloadSpeed { get; init; }
    public long UploadSpeed { get; init; }
    public long Eta { get; init; }
    public long Size { get; init; }
    public DateTimeOffset AddedOn { get; init; }
    public string Category { get; init; } = "";
    public string Tags { get; init; } = "";
}
