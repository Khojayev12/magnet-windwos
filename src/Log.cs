namespace SnapKeys;

/// <summary>Plain-text log of errors plus start and exit, capped at 512 KB.</summary>
internal static class Log
{
    const long MaxBytes = 512 * 1024;

    public static string DirectoryPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Config.AppName);

    static string FilePath => Path.Combine(DirectoryPath, "log.txt");

    public static void Info(string message) => Write("INFO ", message);

    public static void Error(string message) => Write("ERROR", message);

    static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var file = new FileInfo(FilePath);
            var mode = file.Exists && file.Length >= MaxBytes ? FileMode.Create : FileMode.Append;
            using var stream = new FileStream(FilePath, mode, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);
            writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}");
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
