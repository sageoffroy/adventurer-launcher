using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace AdventurerLauncher.Services;

public sealed class SelfUpdateService
{
    private const string VersionUrl = "https://raw.githubusercontent.com/sageoffroy/adventurer-launcher/feature/launcher-v1/distribution/launcher-version.json";
    private readonly HttpClient _httpClient;

    public SelfUpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> TryStartUpdateAsync(CancellationToken cancellationToken = default)
    {
        LauncherVersionInfo? remote;
        try
        {
            using var response = await _httpClient.GetAsync(VersionUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            remote = await JsonSerializer.DeserializeAsync<LauncherVersionInfo>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, cancellationToken);
        }
        catch
        {
            // A launcher update check must never prevent the game launcher from opening.
            return false;
        }

        if (remote is null ||
            !Version.TryParse(remote.Version, out var remoteVersion) ||
            !Uri.TryCreate(remote.Url, UriKind.Absolute, out var installerUri) ||
            installerUri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(remote.Sha256))
        {
            return false;
        }

        var localVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        if (remoteVersion <= localVersion)
            return false;

        var tempInstaller = Path.Combine(Path.GetTempPath(), $"AventurerosLauncherSetup-{remote.Version}.exe");

        try
        {
            using (var response = await _httpClient.GetAsync(installerUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var destination = new FileStream(tempInstaller, FileMode.Create, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(destination, cancellationToken);
            }

            var actualHash = await ComputeSha256Async(tempInstaller, cancellationToken);
            if (!actualHash.Equals(remote.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(tempInstaller);
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = tempInstaller,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                UseShellExecute = true
            });

            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(tempInstaller))
                    File.Delete(tempInstaller);
            }
            catch
            {
            }
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

    private sealed class LauncherVersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }
}
