using System.Net.Http;
using System.Text.Json;
using AdventurerLauncher.Models;

namespace AdventurerLauncher.Services;

public sealed class RemoteContentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<LauncherConfig> LoadConfigAsync()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "launcher-config.json");
        await using var stream = File.OpenRead(configPath);
        return await JsonSerializer.DeserializeAsync<LauncherConfig>(stream, JsonOptions)
               ?? throw new InvalidDataException("No se pudo leer launcher-config.json.");
    }

    public async Task<LauncherManifest> LoadManifestAsync(string url)
    {
        EnsureHttpUrl(url, "manifest");
        return await GetJsonAsync<LauncherManifest>(url);
    }

    public async Task<List<NewsItem>> LoadNewsAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return [];
        EnsureHttpUrl(url, "noticias");
        return await GetJsonAsync<List<NewsItem>>(url);
    }

    public async Task<List<ChangelogEntry>> LoadChangelogAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return [];
        EnsureHttpUrl(url, "changelog");
        return await GetJsonAsync<List<ChangelogEntry>>(url);
    }

    public HttpClient HttpClient => _httpClient;

    private async Task<T> GetJsonAsync<T>(string url)
    {
        await using var stream = await _httpClient.GetStreamAsync(url);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions)
               ?? throw new InvalidDataException($"Respuesta JSON inválida: {url}");
    }

    private static void EnsureHttpUrl(string url, string label)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidDataException($"La URL de {label} no está configurada correctamente.");
    }
}
