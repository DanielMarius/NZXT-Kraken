using System.Text;
using System.Text.Json;

internal static class ControllerFiles
{
    public static bool TryEnsureDirectory(string path)
    {
        try { Directory.CreateDirectory(path); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // A failed publication leaves the previous complete snapshot visible. Readers
    // must use its original timestamp, never its last attempted publication time.
    public static bool TryWriteJson<T>(string path, T value)
    {
        var temporaryPath = path + $".{Environment.ProcessId}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value), new UTF8Encoding(false));
            File.Move(temporaryPath, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            return false;
        }
    }
}

internal static class ControllerLog
{
    internal const long MaxActiveBytes = 8 * 1024 * 1024;
    private static readonly object Gate = new();

    public static void Write(string path, string message)
    {
        lock (Gate)
        {
            try
            {
                // Retain old logs rather than deleting user evidence. Only the
                // active file is capped; archive retirement is an operator choice.
                if (File.Exists(path) && new FileInfo(path).Length >= MaxActiveBytes)
                    File.Move(path, path + $".{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}.{Guid.NewGuid():N}.archive");

                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}", new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A full disk, locked file or denied log path must not stop cooling.
            }
        }
    }
}
