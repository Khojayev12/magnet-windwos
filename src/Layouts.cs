namespace MagnetWindows;

public enum SnapAction
{
    Left, Right, Top, Bottom,
    TopLeft, TopRight, BottomLeft, BottomRight,
    LeftThird, CenterThird, RightThird,
    LeftTwoThirds, CenterTwoThirds, RightTwoThirds,
    NextDisplay, PreviousDisplay,
    Maximize, Center, Restore,
}

/// <summary>Screen rectangle in physical pixels. Right and Bottom are exclusive, as in a Win32 RECT.</summary>
public readonly record struct Rect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>Pure geometry: (work area, action) -> target rectangle. No Win32 calls, so every action is unit tested.</summary>
public static class Layouts
{
    /// <summary>Actions whose target depends only on the work area.</summary>
    public static IReadOnlyList<SnapAction> FixedLayouts { get; } =
    [
        SnapAction.Left, SnapAction.Right, SnapAction.Top, SnapAction.Bottom,
        SnapAction.TopLeft, SnapAction.TopRight, SnapAction.BottomLeft, SnapAction.BottomRight,
        SnapAction.LeftThird, SnapAction.CenterThird, SnapAction.RightThird,
        SnapAction.LeftTwoThirds, SnapAction.CenterTwoThirds, SnapAction.RightTwoThirds,
        SnapAction.Maximize,
    ];

    public static Rect Target(Rect area, SnapAction action) => action switch
    {
        SnapAction.Left => Cell(area, 0, 1, 2, 0, 1, 1),
        SnapAction.Right => Cell(area, 1, 2, 2, 0, 1, 1),
        SnapAction.Top => Cell(area, 0, 1, 1, 0, 1, 2),
        SnapAction.Bottom => Cell(area, 0, 1, 1, 1, 2, 2),
        SnapAction.TopLeft => Cell(area, 0, 1, 2, 0, 1, 2),
        SnapAction.TopRight => Cell(area, 1, 2, 2, 0, 1, 2),
        SnapAction.BottomLeft => Cell(area, 0, 1, 2, 1, 2, 2),
        SnapAction.BottomRight => Cell(area, 1, 2, 2, 1, 2, 2),
        SnapAction.LeftThird => Cell(area, 0, 1, 3, 0, 1, 1),
        SnapAction.CenterThird => Cell(area, 1, 2, 3, 0, 1, 1),
        SnapAction.RightThird => Cell(area, 2, 3, 3, 0, 1, 1),
        SnapAction.LeftTwoThirds => Cell(area, 0, 2, 3, 0, 1, 1),
        SnapAction.CenterTwoThirds => Cell(area, 1, 5, 6, 0, 1, 1),
        SnapAction.RightTwoThirds => Cell(area, 1, 3, 3, 0, 1, 1),
        SnapAction.Maximize => area,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Not a fixed layout."),
    };

    /// <summary>Keeps the window's size and centers it, shrinking it first if it is larger than the area.</summary>
    public static Rect Center(Rect area, Rect window)
    {
        int width = Math.Min(window.Width, area.Width);
        int height = Math.Min(window.Height, area.Height);
        int left = area.Left + (area.Width - width) / 2;
        int top = area.Top + (area.Height - height) / 2;
        return new(left, top, left + width, top + height);
    }

    /// <summary>Carries a window to another work area, keeping its position and size relative to the area.</summary>
    public static Rect MoveToArea(Rect window, Rect from, Rect to)
    {
        // A window sitting exactly on a layout keeps that layout, so a snapped half lands as an exact half.
        foreach (var action in FixedLayouts)
        {
            if (Target(from, action) == window)
                return Target(to, action);
        }

        var scaled = new Rect(
            Scale(window.Left, from.Left, from.Width, to.Left, to.Width),
            Scale(window.Top, from.Top, from.Height, to.Top, to.Height),
            Scale(window.Right, from.Left, from.Width, to.Left, to.Width),
            Scale(window.Bottom, from.Top, from.Height, to.Top, to.Height));

        int width = Math.Clamp(scaled.Width, 1, to.Width);
        int height = Math.Clamp(scaled.Height, 1, to.Height);
        int left = Math.Clamp(scaled.Left, to.Left, to.Right - width);
        int top = Math.Clamp(scaled.Top, to.Top, to.Bottom - height);
        return new(left, top, left + width, top + height);
    }

    /// <summary>
    /// Where a window that would not shrink to target (an app minimum size) should sit: against the work-area edge
    /// the target touches, centered on the target otherwise, and never outside the area (so never under the taskbar).
    /// </summary>
    public static Rect Settle(Rect target, Rect actual, Rect area)
    {
        int left = SettleAxis(target.Left, target.Right, actual.Width, area.Left, area.Right);
        int top = SettleAxis(target.Top, target.Bottom, actual.Height, area.Top, area.Bottom);
        return new(left, top, left + actual.Width, top + actual.Height);
    }

    static int SettleAxis(int start, int end, int size, int areaStart, int areaEnd)
    {
        if (size <= end - start)
            return start;
        bool atStart = start == areaStart;
        bool atEnd = end == areaEnd;
        int position = atEnd && !atStart ? end - size
            : atStart && !atEnd ? start
            : start - (size - (end - start)) / 2;
        return Math.Clamp(position, areaStart, Math.Max(areaStart, areaEnd - size));
    }

    /// <summary>Columns x0..x1 out of xParts and rows y0..y1 out of yParts.</summary>
    static Rect Cell(Rect area, int x0, int x1, int xParts, int y0, int y1, int yParts) => new(
        Edge(area.Left, area.Width, x0, xParts),
        Edge(area.Top, area.Height, y0, yParts),
        Edge(area.Left, area.Width, x1, xParts),
        Edge(area.Top, area.Height, y1, yParts));

    // Every edge comes from the same integer formula, so neighbours share it exactly (no gap, no overlap)
    // and n == parts lands on the far edge of the area.
    static int Edge(int start, int length, int n, int parts) => start + (int)((long)length * n / parts);

    static int Scale(int value, int fromStart, int fromLength, int toStart, int toLength) =>
        toStart + (int)Math.Round((double)(value - fromStart) * toLength / fromLength);
}
