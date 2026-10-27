using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace FloatingClock.Services;

public static class ErrorLogger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FloatingClock", "Logs");

    private static readonly string LogFile;
    private static readonly object Lock = new();

    static ErrorLogger()
    {
        Directory.CreateDirectory(LogDir);
        LogFile = Path.Combine(LogDir, $"FloatingClock_{DateTime.Now:yyyy-MM-dd}.log");
    }

    public static void LogError(Exception ex, string context = "",
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        var msg = $"[ERROR] [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{Path.GetFileName(file)}:{line} {caller}] {context}\n" +
                  $"  Exception: {ex.GetType().Name}: {ex.Message}\n" +
                  $"  StackTrace: {ex.StackTrace}";
        if (ex.InnerException != null)
            msg += $"\n  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        Write(msg);
    }

    public static void LogInfo(string message, [CallerMemberName] string caller = "")
        => Write($"[INFO]  [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{caller}] {message}");

    public static void LogWarning(string message, [CallerMemberName] string caller = "")
        => Write($"[WARN]  [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{caller}] {message}");

    private static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                File.AppendAllText(LogFile, message + Environment.NewLine);
            }
        }
        catch { /* swallow – can't log logging failures */ }
    }

    public static string GetLogDirectory() => LogDir;
}
