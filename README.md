# Magnet Windows

A small Windows tray app that snaps the active window to halves, quarters, thirds and two-thirds of its monitor with keyboard shortcuts, modeled on the layout menu of the macOS app [Magnet](https://magnet.crowdcafe.com/).

- Keyboard only: every shortcut is set in source, with no settings window or config file.
- Starts with Windows, lives in the system tray, and idles at 0% CPU.
- Leaves other apps alone: it uses no keyboard hooks, never changes focus, and needs no admin rights.
- Ships as a single self-contained `.exe` with nothing to install.

## Shortcuts

All shortcuts use **Ctrl+Alt**. Windows snap to the monitor's work area, so the taskbar is never covered.

| Action | Keys |
| --- | --- |
| Left / Right half | Ctrl+Alt+← / → |
| Top / Bottom half | Ctrl+Alt+↑ / ↓ |
| Top-left / Top-right quarter | Ctrl+Alt+U / I |
| Bottom-left / Bottom-right quarter | Ctrl+Alt+J / K |
| Left / Center / Right third | Ctrl+Alt+D / F / G |
| Left / Center / Right two-thirds | Ctrl+Alt+E / R / T |
| Maximize | Ctrl+Alt+Enter |
| Center | Ctrl+Alt+C |
| Restore (size before the first snap) | Ctrl+Alt+Backspace |
| Next / Previous display | Ctrl+Alt+Shift+→ / ← |

To change a shortcut, edit its line in [`src/Config.cs`](src/Config.cs) and rebuild.

The tray icon's menu has three items: **Enabled**, **Run at startup** (on after the first launch), and **Exit**.

## Build

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows 10 22H2 or Windows 11 (x64).

```bash
dotnet publish src/MagnetWindows.csproj -c Release -o publish
```

This produces `publish/MagnetWindows.exe`, a trimmed, self-contained single-file exe of about 12 MB. If the MSVC build tools are installed, add `-p:PublishAot=true` to get a smaller Native AOT exe.

Run the geometry tests:

```bash
dotnet test tests/Layouts.Tests/Layouts.Tests.csproj
```

## Notes

- The Fn key can't be part of a shortcut, because Windows never receives Fn key events.
- On keyboard layouts that use AltGr, Windows reports AltGr as Ctrl+Alt, so a Ctrl+Alt+letter shortcut can block typing that character. If that happens, change `BaseModifiers` in `Config.cs`.
- If an app has a minimum size larger than the slot (Telegram, for example), the window keeps that size. It's placed against the slot's screen edge and stays inside the work area.
- Windows of apps running as administrator can't be moved by a standard-user app. Those snaps are skipped and logged.
- Errors go to `%LOCALAPPDATA%\Magnet Windows\log.txt`.

## Project layout

| Path | Contents |
| --- | --- |
| `src/Layouts.cs` | Pure geometry (work area + action → rectangle), unit tested |
| `src/WindowMover.cs` | Window checks, invisible-border and DPI handling, `SetWindowPos` |
| `src/HotkeyManager.cs`, `src/Config.cs` | `RegisterHotKey` bindings |
| `src/TrayIcon.cs`, `src/Program.cs` | Tray icon, menu, message loop, single instance |
| `src/Startup.cs`, `src/Log.cs` | Run-at-login registry value, capped log |
| `tests/Layouts.Tests` | xUnit tests for the geometry |

Magnet Windows is an independent project and is not affiliated with Magnet or its developer, Crowd Cafe.
