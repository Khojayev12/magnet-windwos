using System.ComponentModel;
using System.Runtime.InteropServices;
using static SnapKeys.Native;

namespace SnapKeys;

internal static unsafe class Program
{
    const string MutexName = @"Local\SnapKeys.SingleInstance.6f1c2b7e";
    const string WindowClass = "SnapKeys.Host";

    static nint _hwnd;
    static bool _enabled;
    static bool _conflictBalloonShown;
    static bool _shutDown;

    public static string Version { get; } = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    static int Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
            return 0; // Already running: exit silently.

        try
        {
            Log.Info($"Start {Config.AppName} {Version}.");
            try
            {
                Startup.Initialize();
            }
            catch (Exception e)
            {
                Log.Error($"Run-at-startup setup failed: {e.Message}");
            }

            _hwnd = CreateHostWindow();
            TrayIcon.Create(_hwnd);
            EnableHotkeys();

            MSG msg;
            while (GetMessageW(&msg, 0, 0, 0) > 0)
            {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
        }
        catch (Exception e)
        {
            Log.Error($"Fatal: {e}");
        }
        finally
        {
            ShutDown();
            mutex.ReleaseMutex();
        }
        return 0;
    }

    static nint CreateHostWindow()
    {
        nint instance = GetModuleHandleW(0);
        fixed (char* className = WindowClass)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = instance,
                lpszClassName = className,
            };
            if (RegisterClassExW(&wc) == 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "RegisterClassEx failed.");
        }

        // Hidden top-level window, not message-only: only top-level windows receive the TaskbarCreated broadcast.
        // Never shown, and a tool window, so it has no taskbar button.
        nint hwnd = CreateWindowExW(WS_EX_TOOLWINDOW, WindowClass, Config.AppName, WS_POPUP, 0, 0, 0, 0, 0, 0, instance, 0);
        if (hwnd == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreateWindowEx failed.");
        return hwnd;
    }

    [UnmanagedCallersOnly]
    static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        // Nothing may escape a native callback; one misbehaving window must not take the process down.
        try
        {
            switch (msg)
            {
                case WM_HOTKEY:
                    if (_enabled && HotkeyManager.TryGetAction((int)wParam, out var action))
                        WindowMover.Perform(action);
                    return 0;

                case TrayIcon.CallbackMessage:
                    if (TrayIcon.IsMenuRequest(lParam))
                        OnMenu(TrayIcon.ShowMenu(wParam, _enabled, Startup.IsEnabled));
                    return 0;

                case WM_ENDSESSION:
                    if (wParam != 0)
                        ShutDown();
                    return 0;

                case WM_DESTROY:
                    ShutDown();
                    PostQuitMessage(0);
                    return 0;
            }

            if (msg != 0 && msg == TrayIcon.TaskbarCreatedMessage)
            {
                TrayIcon.Recreate();
                return 0;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Handling message 0x{msg:X4} failed: {e}");
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    static void OnMenu(TrayIcon.MenuCommand command)
    {
        switch (command)
        {
            case TrayIcon.MenuCommand.ToggleEnabled:
                if (_enabled)
                {
                    HotkeyManager.UnregisterAll(_hwnd);
                    _enabled = false;
                }
                else
                {
                    EnableHotkeys();
                }
                break;

            case TrayIcon.MenuCommand.ToggleStartup:
                Startup.SetEnabled(!Startup.IsEnabled);
                break;

            case TrayIcon.MenuCommand.Exit:
                DestroyWindow(_hwnd);
                break;
        }
    }

    static void EnableHotkeys()
    {
        _enabled = true;
        var failed = HotkeyManager.RegisterAll(_hwnd);
        if (failed.Count == 0)
            return;

        // Another app owns these; skip them and keep the rest rather than fight over them.
        Log.Error($"Hotkeys already in use by another app: {string.Join(", ", failed)}.");
        if (!_conflictBalloonShown)
        {
            _conflictBalloonShown = true;
            TrayIcon.ShowBalloon($"{Config.AppName}: some shortcuts are unavailable",
                $"{failed.Count} shortcut(s) are already used by another app: {string.Join(", ", failed)}");
        }
    }

    static void ShutDown()
    {
        if (_shutDown)
            return;
        _shutDown = true;
        HotkeyManager.UnregisterAll(_hwnd);
        TrayIcon.Remove();
        Log.Info("Exit.");
    }
}
