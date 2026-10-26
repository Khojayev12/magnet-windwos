using System.Runtime.InteropServices;
using static MagnetWindows.Native;

namespace MagnetWindows;

internal readonly record struct DisplayInfo(nint Handle, Rect Bounds, Rect WorkArea);

/// <summary>Monitor lookups. Always read at action time: monitors come, go and change scale while the app runs.</summary>
internal static unsafe class MonitorHelper
{
    public static DisplayInfo FromWindow(nint hwnd) => Describe(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST));

    public static DisplayInfo FromRect(Rect rect)
    {
        var native = new RECT(rect);
        return Describe(MonitorFromRect(&native, MONITOR_DEFAULTTONEAREST));
    }

    /// <summary>All monitors, ordered left to right, then top to bottom.</summary>
    public static List<DisplayInfo> All()
    {
        var handles = new List<nint>();
        var gcHandle = GCHandle.Alloc(handles);
        try
        {
            EnumDisplayMonitors(0, null, &Collect, GCHandle.ToIntPtr(gcHandle));
        }
        finally
        {
            gcHandle.Free();
        }
        return handles.Select(Describe).OrderBy(d => d.Bounds.Left).ThenBy(d => d.Bounds.Top).ToList();
    }

    public static uint Dpi(nint monitor)
    {
        uint dpiX, dpiY;
        return GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, &dpiX, &dpiY) == 0 ? dpiX : 96;
    }

    [UnmanagedCallersOnly]
    static int Collect(nint monitor, nint hdc, RECT* clip, nint data)
    {
        ((List<nint>)GCHandle.FromIntPtr(data).Target!).Add(monitor);
        return 1;
    }

    static DisplayInfo Describe(nint monitor)
    {
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfoW(monitor, &info);
        return new(monitor, info.rcMonitor.ToRect(), info.rcWork.ToRect());
    }
}
