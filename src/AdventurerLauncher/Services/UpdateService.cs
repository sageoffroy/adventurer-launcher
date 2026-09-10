using System.Security.Cryptography;
using AdventurerLauncher.Models;

namespace AdventurerLauncher.Services;

public sealed class UpdateService(HttpClient httpClient)
{
    public async Task<List<LauncherFile>> GetOutdatedFilesAsync(
        string gamePath,
        LauncherManifest manifest,
        CancellationToken cancellationToken = default)
    {
        var result = new List<LauncherFile>();

        foreach (var file in manifest.Files)
        {
            var targetPath = ResolveSafeGamePath(gamePath, file.Path);
            if (!File.Exists(targetPath))
            {
                result.Add(file);
                continue;
            }

            var hash = await ComputeSha256Async(targetPath, cancellationToken);
            if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                result.Add(file);
        }

        return result;
    }

    public async Task DownloadAndInstallAsync(
        string gamePath,
        LauncherFile file,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(file.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidDataException($"URL inválida para {file.Path}.");

        var targetPath = ResolveSafeGamePath(gamePath, file.Path);
        var directory = Path.GetDirectoryName(targetPath)
                        ?? throw new InvalidDataException($"Ruta inválida: {file.Path}");
        Directory.CreateDirectory(directory);

        var tempPath = targetPath + ".download";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        try
        {
            using var response = await httpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 128,
                useAsync: true);

            var buffer = new byte[1024 * 128];
            long copied = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                copied += read;
                if (total is > 0)
                    progress?.Report((double)copied / total.Value);
            }

            await destination.FlushAsync(cancellationToken);

            var downloadedHash = await ComputeSha256Async(tempPath, cancellationToken);
            if (!downloadedHash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"La verificación de {file.Path} falló. El archivo descargado no coincide con el publicado.");

            File.Move(tempPath, targetPath, overwrite: true);
            progress?.Report(1);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    public static bool IsValidGameFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;

        return File.Exists(Path.Combine(path, "Wow.exe")) &&
               Directory.Exists(Path.Combine(path, "Data"));
    }

    private static string ResolveSafeGamePath(string gamePath, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new InvalidDataException($"El manifest contiene una ruta absoluta no permitida: {relativePath}");

        var gameRoot = Path.GetFullPath(gamePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(gameRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!target.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"El manifest intenta escribir fuera de la carpeta del juego: {relativePath}");

        return target;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 128,
            useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
