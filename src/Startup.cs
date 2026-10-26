using Microsoft.Win32;

namespace SnapKeys;

/// <summary>Run-at-login through the per-user Run key, which needs no admin rights.</summary>
internal static class Startup
{
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    static string Command => $"\"{Environment.ProcessPath}\"";

    static string FirstRunMarker => Path.Combine(Log.DirectoryPath, "first-run.done");

    public static bool IsEnabled => ReadValue() is not null;

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(Config.AppName, Command);
        else
            key.DeleteValue(Config.AppName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Turns run-at-startup on after the first launch only, so a later "off" sticks; on every launch
    /// repoints an existing value at this exe in case it has moved.
    /// </summary>
    public static void Initialize()
    {
        if (!File.Exists(FirstRunMarker))
        {
            SetEnabled(true);
            Directory.CreateDirectory(Log.DirectoryPath);
            File.WriteAllText(FirstRunMarker, "");
            return;
        }

        string? current = ReadValue();
        if (current is not null && !string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
            SetEnabled(true);
    }

    static string? ReadValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(Config.AppName) as string;
    }
}
