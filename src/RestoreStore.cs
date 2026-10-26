namespace SnapKeys;

/// <summary>Remembers each window's rectangle and maximized state from before its first snap, keyed by HWND.</summary>
internal static class RestoreStore
{
    const int Capacity = 64;

    sealed class Entry
    {
        public Rect Original;
        public bool WasMaximized;
        public Rect LastPlaced;
        public long Stamp;
    }

    static readonly Dictionary<nint, Entry> Entries = [];
    static long _clock;

    /// <summary>True when the window is still exactly where the app last put it, so the earlier entry should be kept.</summary>
    public static bool IsWhereWeLeftIt(nint hwnd, Rect outer) =>
        Entries.TryGetValue(hwnd, out var entry) && entry.LastPlaced == outer;

    public static void Save(nint hwnd, Rect original, bool wasMaximized)
    {
        Prune();
        Entries[hwnd] = new Entry { Original = original, WasMaximized = wasMaximized, Stamp = ++_clock };
    }

    public static void MarkPlaced(nint hwnd, Rect outer)
    {
        if (Entries.TryGetValue(hwnd, out var entry))
            entry.LastPlaced = outer;
    }

    public static bool TryTake(nint hwnd, out Rect original, out bool wasMaximized)
    {
        if (Entries.Remove(hwnd, out var entry))
        {
            (original, wasMaximized) = (entry.Original, entry.WasMaximized);
            return true;
        }
        (original, wasMaximized) = (default, false);
        return false;
    }

    // Drop closed windows first, then the oldest entries, so a new one always fits under the cap.
    static void Prune()
    {
        foreach (var hwnd in Entries.Keys.Where(h => !Native.IsWindow(h)).ToList())
            Entries.Remove(hwnd);

        while (Entries.Count >= Capacity)
            Entries.Remove(Entries.MinBy(e => e.Value.Stamp).Key);
    }
}
