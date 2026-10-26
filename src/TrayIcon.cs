using static SnapKeys.Native;

namespace SnapKeys;

/// <summary>The app's only visible surface: a notification-area icon with a three-item menu.</summary>
internal static unsafe class TrayIcon
{
    public enum MenuCommand { None, ToggleEnabled, ToggleStartup, Exit }

    public const uint CallbackMessage = WM_APP + 1;
    const uint IconId = 1;

    // The SDK embeds ApplicationIcon in the exe as icon group 32512.
    const nint AppIconResource = 32512;

    static nint _hwnd;
    static nint _icon;
    static bool _added;
    static (string Title, string Text)? _pendingBalloon;

    /// <summary>Broadcast by Explorer whenever the taskbar is (re)created; zero until Create runs.</summary>
    public static uint TaskbarCreatedMessage { get; private set; }

    public static void Create(nint hwnd)
    {
        _hwnd = hwnd;
        TaskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
        _icon = LoadImageW(GetModuleHandleW(0), AppIconResource, IMAGE_ICON,
            GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), 0);
        if (_icon == 0)
            _icon = LoadIconW(0, IDI_APPLICATION);
        Add();
    }

    /// <summary>Explorer restarted (or started late at login) and every tray icon is gone: add ours again.</summary>
    public static void Recreate()
    {
        _added = false;
        Add();
    }

    public static void Remove()
    {
        if (!_added)
            return;
        var data = NewData(0);
        Shell_NotifyIconW(NIM_DELETE, &data);
        _added = false;
    }

    public static void ShowBalloon(string title, string text)
    {
        if (!_added)
        {
            _pendingBalloon = (title, text);
            return;
        }
        var data = NewData(NIF_INFO);
        Copy(title, data.szInfoTitle, 64);
        Copy(text, data.szInfo, 256);
        data.dwInfoFlags = NIIF_WARNING;
        Shell_NotifyIconW(NIM_MODIFY, &data);
    }

    public static bool IsMenuRequest(nint lParam) =>
        (uint)(lParam & 0xFFFF) is WM_CONTEXTMENU or NIN_SELECT or NIN_KEYSELECT;

    /// <summary>Shows the menu at the anchor point packed into wParam (NOTIFYICON_VERSION_4) and returns the choice.</summary>
    public static MenuCommand ShowMenu(nuint wParam, bool enabled, bool runAtStartup)
    {
        int x = (short)(wParam & 0xFFFF);
        int y = (short)((wParam >> 16) & 0xFFFF);
        nint menu = CreatePopupMenu();
        try
        {
            AppendMenuW(menu, MF_STRING | (enabled ? MF_CHECKED : 0), (nuint)MenuCommand.ToggleEnabled, "Enabled");
            AppendMenuW(menu, MF_STRING | (runAtStartup ? MF_CHECKED : 0), (nuint)MenuCommand.ToggleStartup, "Run at startup");
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            AppendMenuW(menu, MF_STRING, (nuint)MenuCommand.Exit, "Exit");

            // The owner must be foreground or the menu stays open when the user clicks elsewhere.
            SetForegroundWindow(_hwnd);
            int command = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY, x, y, 0, _hwnd, null);
            PostMessageW(_hwnd, WM_NULL, 0, 0);
            return (MenuCommand)command;
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    static void Add()
    {
        var data = NewData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _icon;
        Copy($"{Config.AppName} {Program.Version}", data.szTip, 128);
        _added = Shell_NotifyIconW(NIM_ADD, &data) || Shell_NotifyIconW(NIM_MODIFY, &data);
        if (!_added)
            return; // Shell not up yet; TaskbarCreated will arrive once it is.

        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, &data);

        if (_pendingBalloon is { } balloon)
        {
            _pendingBalloon = null;
            ShowBalloon(balloon.Title, balloon.Text);
        }
    }

    static NOTIFYICONDATAW NewData(uint flags) => new()
    {
        cbSize = (uint)sizeof(NOTIFYICONDATAW),
        hWnd = _hwnd,
        uID = IconId,
        uFlags = flags,
    };

    static void Copy(string text, char* destination, int capacity)
    {
        int length = Math.Min(text.Length, capacity - 1);
        text.AsSpan(0, length).CopyTo(new Span<char>(destination, capacity));
        destination[length] = '\0';
    }
}
