using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace GaimerDesktop.Services.Chess;

/// <summary>
/// Downloads and installs the Stockfish binary on-demand.
/// Platform-aware: selects correct binary for macOS ARM64/x64 and Windows x64.
/// </summary>
public sealed class StockfishDownloader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StockfishDownloader>? _logger;

    public StockfishDownloader(HttpClient httpClient, ILogger<StockfishDownloader>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// The path where the Stockfish binary should be stored.
    /// </summary>
    public static string GetEnginePath()
    {
        string appData;
        string binaryName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            binaryName = "stockfish.exe";
            return Path.Combine(appData, "GaimerDesktop", "engines", binaryName);
        }
        else
        {
#if ANDROID || IOS || MACCATALYST || WINDOWS
            appData = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
#else
            // Non-MAUI (test runner): use macOS standard app support directory
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            appData = Path.Combine(home, "Library", "Application Support");
#endif
            binaryName = "stockfish";
            return Path.Combine(appData, "engines", binaryName);
        }
    }

    /// <summary>
    /// Returns true if Stockfish binary exists at the expected path.
    /// </summary>
    public static bool IsInstalled() => File.Exists(GetEnginePath());

    /// <summary>
    /// Downloads, verifies, and installs the Stockfish binary.
    /// Uses temp file + atomic rename pattern.
    /// </summary>
    public async Task<bool> DownloadAsync(
        string downloadUrl,
        string? expectedSha256 = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var enginePath = GetEnginePath();
        var engineDir = Path.GetDirectoryName(enginePath)!;
        Directory.CreateDirectory(engineDir);

        var tempPath = enginePath + ".tmp";

        try
        {
            _logger?.LogInformation("[StockfishDownloader] Downloading from {Url}", downloadUrl);

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            long downloadedBytes = 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                    progress?.Report((double)downloadedBytes / totalBytes);
            }

            progress?.Report(1.0);
            fileStream.Close();

            // SHA256 verification
            if (!string.IsNullOrEmpty(expectedSha256))
            {
                var actualHash = await ComputeSha256Async(tempPath, ct);
                if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogError("[StockfishDownloader] SHA256 mismatch: expected={Expected} actual={Actual}",
                        expectedSha256, actualHash);
                    File.Delete(tempPath);
                    return false;
                }
            }

            // Atomic rename
            if (File.Exists(enginePath))
                File.Delete(enginePath);
            File.Move(tempPath, enginePath);

            // chmod +x on macOS
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await SetExecutableAsync(enginePath, ct);
            }

            _logger?.LogInformation("[StockfishDownloader] Installed to {Path}", enginePath);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "[StockfishDownloader] Download failed");
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            return false;
        }
    }

    /// <summary>
    /// Gets the appropriate download URL for the current platform from GitHub releases.
    /// </summary>
    public static string GetAssetName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "stockfish-windows-x86-64-bmi2.zip";

        if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            return "stockfish-macos-m1-apple-silicon.tar";

        return "stockfish-macos-x86-64-bmi2.tar";
    }

    private static async Task SetExecutableAsync(string path, CancellationToken ct)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"+x \"{path}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is not null)
            await process.WaitForExitAsync(ct);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}
