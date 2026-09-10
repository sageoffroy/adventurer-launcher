using System.Text.Json;
using AdventurerLauncher.Models;

namespace AdventurerLauncher.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public SettingsService()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AventurerosLauncher");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public async Task<LauncherSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
            return new LauncherSettings();

        await using var stream = File.OpenRead(_settingsPath);
        return await JsonSerializer.DeserializeAsync<LauncherSettings>(stream)
               ?? new LauncherSettings();
    }

    public async Task SaveAsync(LauncherSettings settings)
    {
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }
}
