using static SnapKeys.Native;

namespace SnapKeys;

/// <summary>Registers the bindings with RegisterHotKey (never a keyboard hook) and maps WM_HOTKEY ids back to actions.</summary>
internal static class HotkeyManager
{
    // Hotkey id = FirstId + index into Config.Bindings.
    const int FirstId = 1;

    static readonly List<int> Registered = [];

    /// <summary>Registers every binding it can and returns the ones another app already owns.</summary>
    public static List<string> RegisterAll(nint hwnd)
    {
        UnregisterAll(hwnd);
        var failed = new List<string>();
        for (int i = 0; i < Config.Bindings.Length; i++)
        {
            var binding = Config.Bindings[i];
            if (RegisterHotKey(hwnd, FirstId + i, binding.Modifiers | MOD_NOREPEAT, binding.Key))
                Registered.Add(FirstId + i);
            else
                failed.Add($"{Describe(binding)} ({binding.Action})");
        }
        return failed;
    }

    public static void UnregisterAll(nint hwnd)
    {
        foreach (int id in Registered)
            UnregisterHotKey(hwnd, id);
        Registered.Clear();
    }

    public static bool TryGetAction(int id, out SnapAction action)
    {
        int index = id - FirstId;
        if ((uint)index < (uint)Config.Bindings.Length)
        {
            action = Config.Bindings[index].Action;
            return true;
        }
        action = default;
        return false;
    }

    static string Describe(Binding binding)
    {
        var parts = new List<string>();
        if ((binding.Modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((binding.Modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((binding.Modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        parts.Add(binding.KeyName);
        return string.Join('+', parts);
    }
}
