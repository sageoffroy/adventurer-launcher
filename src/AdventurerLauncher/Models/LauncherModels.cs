namespace AdventurerLauncher.Models;

public sealed class LauncherConfig
{
    public string ManifestUrl { get; set; } = string.Empty;
}

public sealed class LauncherSettings
{
    public string GamePath { get; set; } = string.Empty;
}

public sealed class LauncherManifest
{
    public string Version { get; set; } = "0.0.0";
    public string NewsUrl { get; set; } = string.Empty;
    public string ChangelogUrl { get; set; } = string.Empty;
    public List<LauncherFile> Files { get; set; } = [];
}

public sealed class LauncherFile
{
    public string Path { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Size { get; set; }
}

public sealed class NewsItem
{
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class ChangelogEntry
{
    public string Version { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public List<string> Changes { get; set; } = [];
}
