using static MagnetWindows.Native;

namespace MagnetWindows;

internal readonly record struct Binding(SnapAction Action, uint Modifiers, uint Key, string KeyName);

/// <summary>Every shortcut lives here. To change one, edit its line and rebuild; there is no runtime configuration.</summary>
internal static class Config
{
    // Shown in the tray tooltip and balloon; also names the Run value and the %LOCALAPPDATA% folder.
    public const string AppName = "Magnet Windows";

    // Fn never reaches Windows (keyboards handle it in firmware), so Ctrl+Alt stands in for Magnet's Ctrl+Option.
    public const uint BaseModifiers = MOD_CONTROL | MOD_ALT;

    // Magnet adds Cmd for display moves; many Win-key combos are reserved by Windows, so Shift is used instead.
    public const uint DisplayExtraModifier = MOD_SHIFT;

    public static readonly Binding[] Bindings =
    [
        new(SnapAction.Left, BaseModifiers, VK_LEFT, "Left"),
        new(SnapAction.Right, BaseModifiers, VK_RIGHT, "Right"),
        new(SnapAction.Top, BaseModifiers, VK_UP, "Up"),
        new(SnapAction.Bottom, BaseModifiers, VK_DOWN, "Down"),
        new(SnapAction.TopLeft, BaseModifiers, 'U', "U"),
        new(SnapAction.TopRight, BaseModifiers, 'I', "I"),
        new(SnapAction.BottomLeft, BaseModifiers, 'J', "J"),
        new(SnapAction.BottomRight, BaseModifiers, 'K', "K"),
        new(SnapAction.LeftThird, BaseModifiers, 'D', "D"),
        new(SnapAction.CenterThird, BaseModifiers, 'F', "F"),
        new(SnapAction.RightThird, BaseModifiers, 'G', "G"),
        new(SnapAction.LeftTwoThirds, BaseModifiers, 'E', "E"),
        new(SnapAction.CenterTwoThirds, BaseModifiers, 'R', "R"),
        new(SnapAction.RightTwoThirds, BaseModifiers, 'T', "T"),
        new(SnapAction.NextDisplay, BaseModifiers | DisplayExtraModifier, VK_RIGHT, "Right"),
        new(SnapAction.PreviousDisplay, BaseModifiers | DisplayExtraModifier, VK_LEFT, "Left"),
        new(SnapAction.Maximize, BaseModifiers, VK_RETURN, "Enter"),
        new(SnapAction.Center, BaseModifiers, 'C', "C"),
        new(SnapAction.Restore, BaseModifiers, VK_BACK, "Backspace"),
    ];
}
