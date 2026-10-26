using System.Runtime.InteropServices;
using static SnapKeys.Native;

namespace SnapKeys;

/// <summary>Applies an action to the foreground window: eligibility checks, border compensation, DPI, SetWindowPos.</summary>
internal static unsafe class WindowMover
{
    const uint MoveFlags = SWP_NOZORDER | SWP_NOACTIVATE;

    static readonly string[] ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    public static void Perform(SnapAction action)
    {
        nint hwnd = GetForegroundWindow();
        if (!CanMove(hwnd))
            return;

        if (action == SnapAction.Restore)
        {
            Restore(hwnd);
            return;
        }

        var display = MonitorHelper.FromWindow(hwnd);
        DisplayInfo? destination = null;
        if (action is SnapAction.NextDisplay or SnapAction.PreviousDisplay)
        {
            var displays = MonitorHelper.All();
            if (displays.Count < 2)
                return;
            int index = displays.FindIndex(d => d.Handle == display.Handle);
            int step = action == SnapAction.NextDisplay ? 1 : -1;
            destination = displays[(index + step + displays.Count) % displays.Count];
        }

        bool maximized = IsZoomed(hwnd);
        Rect outer = GetOuterRect(hwnd);
        bool keepEarlier = !maximized && RestoreStore.IsWhereWeLeftIt(hwnd, outer);
        if (maximized)
        {
            // Windows ignores moves of a maximized window.
            ShowWindow(hwnd, SW_RESTORE);
            outer = GetOuterRect(hwnd);
        }
        if (!keepEarlier)
            RestoreStore.Save(hwnd, outer, maximized);

        Rect area = display.WorkArea;
        Rect frame = maximized ? area : GetVisibleFrame(hwnd);
        Rect target = action switch
        {
            SnapAction.Center => Layouts.Center(area, frame),
            SnapAction.NextDisplay or SnapAction.PreviousDisplay => Layouts.MoveToArea(frame, area, destination!.Value.WorkArea),
            _ => Layouts.Target(area, action),
        };

        Place(hwnd, target, visible: true);
        RestoreStore.MarkPlaced(hwnd, GetOuterRect(hwnd));
    }

    static void Restore(nint hwnd)
    {
        if (!RestoreStore.TryTake(hwnd, out Rect original, out bool wasMaximized))
            return;
        if (IsZoomed(hwnd))
            ShowWindow(hwnd, SW_RESTORE);
        if (Place(hwnd, original, visible: false) && wasMaximized)
            ShowWindow(hwnd, SW_MAXIMIZE);
    }

    static bool CanMove(nint hwnd)
    {
        if (hwnd == 0 || !IsWindowVisible(hwnd) || IsIconic(hwnd))
            return false;

        uint processId;
        GetWindowThreadProcessId(hwnd, &processId);
        if (processId == (uint)Environment.ProcessId)
            return false;

        string className = ClassName(hwnd);
        if (ShellClasses.Contains(className))
            return false;

        long style = GetWindowLongPtrW(hwnd, GWL_STYLE);
        if (IsFullscreen(hwnd, style))
            return false;
        if ((style & WS_THICKFRAME) == 0)
        {
            Log.Error($"Skipped window that is not resizable ({className}).");
            return false;
        }
        return true;
    }

    // Games, videos and F11 browsers drop the caption and cover the whole monitor, taskbar included.
    static bool IsFullscreen(nint hwnd, long style)
    {
        if (IsZoomed(hwnd) || (style & WS_CAPTION) == WS_CAPTION)
            return false;
        Rect outer = GetOuterRect(hwnd);
        Rect screen = MonitorHelper.FromWindow(hwnd).Bounds;
        return outer.Left <= screen.Left && outer.Top <= screen.Top && outer.Right >= screen.Right && outer.Bottom >= screen.Bottom;
    }

    /// <summary>Moves the window so its visible frame (or, with visible false, its outer rect) equals target.</summary>
    static bool Place(nint hwnd, Rect target, bool visible)
    {
        uint dpiBefore = MonitorHelper.Dpi(MonitorHelper.FromWindow(hwnd).Handle);
        uint dpiAfter = MonitorHelper.Dpi(MonitorHelper.FromRect(target).Handle);
        if (!SetBounds(hwnd, target, visible))
            return false;
        // Crossing to a monitor with another DPI makes the app rescale itself after the first move;
        // the second call, with borders measured at the new DPI, settles the final size.
        return dpiBefore == dpiAfter || SetBounds(hwnd, target, visible);
    }

    static bool SetBounds(nint hwnd, Rect target, bool visible)
    {
        Rect r = visible ? ToOuter(hwnd, target) : target;
        if (SetWindowPos(hwnd, 0, r.Left, r.Top, r.Width, r.Height, MoveFlags))
            return true;
        // Typically error 5: the window belongs to an elevated process and UIPI blocks the move.
        Log.Error($"SetWindowPos failed for {ClassName(hwnd)}: error {Marshal.GetLastPInvokeError()}.");
        return false;
    }

    // Windows 10/11 frames carry an invisible resize border (about 7 px); grow the outer rect by it
    // so the visible edge, not the invisible one, lands on the target.
    static Rect ToOuter(nint hwnd, Rect visibleTarget)
    {
        Rect outer = GetOuterRect(hwnd);
        Rect frame = GetVisibleFrame(hwnd);
        return new(
            visibleTarget.Left - (frame.Left - outer.Left),
            visibleTarget.Top - (frame.Top - outer.Top),
            visibleTarget.Right + (outer.Right - frame.Right),
            visibleTarget.Bottom + (outer.Bottom - frame.Bottom));
    }

    static Rect GetOuterRect(nint hwnd)
    {
        RECT r;
        GetWindowRect(hwnd, &r);
        return r.ToRect();
    }

    static Rect GetVisibleFrame(nint hwnd)
    {
        RECT r;
        return DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &r, (uint)sizeof(RECT)) == 0
            ? r.ToRect()
            : GetOuterRect(hwnd);
    }

    static string ClassName(nint hwnd)
    {
        char* buffer = stackalloc char[256];
        int length = GetClassNameW(hwnd, buffer, 256);
        return new string(buffer, 0, length);
    }
}
