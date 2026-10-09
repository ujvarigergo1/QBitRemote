namespace QBitRemote.Models;

public sealed class AppSettings
{
    public string ServerUrl { get; set; } = "http://localhost:8080";
    public string Username { get; set; } = "admin";
    public string? ProtectedPassword { get; set; }
    public string? Language { get; set; }
    public string Theme { get; set; } = "system";
    public int RefreshIntervalSeconds { get; set; } = 1;
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; }
    public bool CloseToTray { get; set; }
    public bool NotifyOnCompletion { get; set; } = true;
    public bool PlayCompletionSound { get; set; }
    public string DefaultSavePath { get; set; } = "";
    public string DefaultCategory { get; set; } = "";
    public string DefaultTags { get; set; } = "";
    public bool AddPausedByDefault { get; set; }
    public bool WarnOnDuplicate { get; set; } = true;
    public double? MainWindowLeft { get; set; }
    public double? MainWindowTop { get; set; }
    public double? MainWindowWidth { get; set; }
    public double? MainWindowHeight { get; set; }
    public bool MainWindowMaximized { get; set; }
    public List<ServerProfile> ServerProfiles { get; set; } = [];
    public List<string> RecentSavePaths { get; set; } = [];
}

public sealed class ServerProfile
{
    public string Name { get; set; } = "Default";
    public string ServerUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string? ProtectedPassword { get; set; }
    public override string ToString() => Name;
}
