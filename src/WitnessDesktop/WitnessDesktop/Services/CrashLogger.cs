using System.Diagnostics;

namespace WitnessDesktop.Services;

/// <summary>
/// File-based crash logger for diagnosing Mac Catalyst window termination.
/// Console.WriteLine may not survive a crash; this writes to a persistent file.
/// </summary>
internal static class CrashLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gaimer",
        "crash.log");

    static CrashLogger()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Logs a lifecycle event (scene changes, window destruction, etc.).
    /// </summary>
    public static void LogLifecycle(string source, string message)
    {
        var line = $"[{DateTime.UtcNow:HH:mm:ss.fff}] [LIFECYCLE] [{source}] {message}";
        Console.WriteLine(line);
        AppendToFile(line);
    }

    /// <summary>
    /// Logs an unhandled exception caught by a global handler.
    /// </summary>
    public static void LogCrash(string source, Exception ex)
    {
        var line = $"[{DateTime.UtcNow:HH:mm:ss.fff}] [CRASH] [{source}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        Console.WriteLine(line);
        AppendToFile(line);
    }

    /// <summary>
    /// Logs an unhandled exception caught inside a MainThread dispatch.
    /// These are the prime suspect for Mac Catalyst window termination.
    /// </summary>
    public static void LogMainThreadException(string callsite, Exception ex)
    {
        var line = $"[{DateTime.UtcNow:HH:mm:ss.fff}] [MAIN_THREAD_CRASH] [{callsite}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        Console.WriteLine(line);
        Debug.WriteLine(line);
        AppendToFile(line);
    }

    /// <summary>
    /// Write session start marker so we can correlate crashes with sessions.
    /// </summary>
    public static void LogSessionStart()
    {
        var line = $"\n=== SESSION START {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} ===";
        AppendToFile(line);
    }

    private static void AppendToFile(string line)
    {
        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch { /* best effort — never crash the crash logger */ }
    }
}
