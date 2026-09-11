using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace AdventurerLauncher.Services;

public sealed class SelfUpdateService
{
    private const string VersionUrl = "https://raw.githubusercontent.com/sageoffroy/adventurer-launcher/feature/launcher-v1/distribution/launcher-version.json";
    private const int MaxAttempts = 3;
    private readonly HttpClient _httpClient;

    public SelfUpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> TryStartUpdateAsync(CancellationToken cancellationToken = default)
    {
        var localVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        Log($"Inicio de comprobación. Versión local: {localVersion}");

        LauncherVersionInfo? remote = null;
        Exception? metadataError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{VersionUrl}?t={cacheBuster}");
                request.Headers.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true
                };

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                remote = await JsonSerializer.DeserializeAsync<LauncherVersionInfo>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }, cancellationToken);

                metadataError = null;
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                metadataError = ex;
                Log($"Intento {attempt}/{MaxAttempts} al consultar versión falló: {ex.Message}");
                if (attempt < MaxAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
        }

        if (metadataError is not null || remote is null)
        {
            Log("No se pudo obtener la versión remota. El launcher continuará normalmente.");
            return false;
        }

        if (!Version.TryParse(remote.Version, out var remoteVersion) ||
            !Uri.TryCreate(remote.Url, UriKind.Absolute, out var installerUri) ||
            installerUri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(remote.Sha256))
        {
            Log($"Metadata de actualización inválida. Versión remota recibida: '{remote.Version}'.");
            return false;
        }

        Log($"Versión remota: {remoteVersion}");

        if (remoteVersion <= localVersion)
        {
            Log("No hay una actualización de launcher pendiente.");
            return false;
        }

        var tempInstaller = Path.Combine(Path.GetTempPath(), $"AventurerosLauncherSetup-{remote.Version}.exe");

        Exception? downloadError = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                Log($"Descargando launcher {remote.Version}. Intento {attempt}/{MaxAttempts}.");
                using var response = await _httpClient.GetAsync(installerUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var destination = new FileStream(tempInstaller, FileMode.Create, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(destination, cancellationToken);

                downloadError = null;
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                downloadError = ex;
                Log($"Intento {attempt}/{MaxAttempts} de descarga falló: {ex.Message}");
                TryDelete(tempInstaller);
                if (attempt < MaxAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }

        if (downloadError is not null || !File.Exists(tempInstaller))
        {
            Log("No se pudo descargar el instalador. El launcher continuará normalmente.");
            return false;
        }

        try
        {
            var actualHash = await ComputeSha256Async(tempInstaller, cancellationToken);
            if (!actualHash.Equals(remote.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Log($"SHA256 inválido. Esperado: {remote.Sha256.Trim()}, obtenido: {actualHash}.");
                TryDelete(tempInstaller);
                return false;
            }

            Log($"SHA256 correcto. Ejecutando instalador {remote.Version}.");

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = tempInstaller,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                UseShellExecute = true
            });

            if (process is null)
            {
                Log("Windows no devolvió un proceso para el instalador.");
                return false;
            }

            Log("Instalador iniciado correctamente. Cerrando launcher actual.");
            return true;
        }
        catch (Exception ex)
        {
            Log($"No se pudo iniciar la actualización: {ex}");
            TryDelete(tempInstaller);
            return false;
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void Log(string message)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AventurerosLauncher");
            Directory.CreateDirectory(directory);

            var logPath = Path.Combine(directory, "launcher-update.log");
            File.AppendAllText(logPath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} | {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private sealed class LauncherVersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }
}
