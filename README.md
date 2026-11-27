# UnshitWindows

A small Windows tray app that adds two things Windows should have had all along:

- **Switch between windows of the same app** with <kbd>Alt</kbd>+<kbd>|</kbd>, the way <kbd>Cmd</kbd>+<kbd>`</kbd> works on macOS.
- **Clipboard history**: your last 10 copies (text, images and files) one shortcut away, in the spirit of ArsClip.

It lives in the system tray. There is no main window, and everything is configurable from a Settings window.

## Requirements

- Windows 10 or 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) to run it
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build it (there are no prebuilt downloads yet)

## Install

Clone the repository and run the publish script from PowerShell:

```powershell
git clone https://github.com/haakony-labs/unshitwin.git
cd unshitwin
.\tools\publish.ps1 -Start
```

This builds a Release version and puts a single `UnshitWindows.exe` in
`%LOCALAPPDATA%\Programs\UnshitWindows`. `-Start` launches it, and the blue icon appears in the tray (it may be
hidden under the **^** arrow; drag it onto the taskbar to keep it visible).

To install somewhere else, pass `-OutDir`:

```powershell
.\tools\publish.ps1 -OutDir 'D:\Tools\UnshitWindows' -Start
```

**Start with Windows:** right-click the tray icon and tick **Start with Windows**. This adds an entry under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` that points to the exe you are running, so turn it on from the
installed copy, not from a build in the source folder.

**Updating:** pull the latest code and run `.\tools\publish.ps1` again. It closes the running copy, replaces the exe,
and starts it again.

## Usage

### Same-app window switching

| Shortcut | What it does |
| --- | --- |
| <kbd>Alt</kbd>+<kbd>\|</kbd> | Switch to the next window of the app you're in |
| <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>\|</kbd> | Same, in the other direction |

- A **single tap** flips between the two most recently used windows of the app.
- **Hold Alt and keep tapping** to walk through all of its windows, most recent first. Release Alt to stay where you
  are.
- Minimized windows are included and are restored when you land on them (you can turn this off).
- Windows of other apps are never selected. If the app has only one window, nothing happens.

The default key is whichever key sits **above Tab** on your keyboard layout: <kbd>|</kbd> on Norwegian,
<kbd>`</kbd> on US English, and so on.

### Clipboard history

Copy as usual. Press <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>V</kbd> to open the picker next to the text cursor (or the
mouse pointer, in apps that don't report a cursor).

| In the picker | |
| --- | --- |
| <kbd>1</kbd>–<kbd>9</kbd>, <kbd>0</kbd> | Paste that entry (<kbd>0</kbd> is the tenth) |
| <kbd>↑</kbd> <kbd>↓</kbd> then <kbd>Enter</kbd>, or click | Paste the selected entry |
| <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>V</kbd> again | Move the selection down |
| <kbd>Del</kbd> | Remove the entry from history |
| <kbd>Esc</kbd>, or click elsewhere | Close without pasting |

The picked entry is pasted into the window you were in and also becomes the current clipboard, so a plain
<kbd>Ctrl</kbd>+<kbd>V</kbd> pastes it again.

What gets kept:

- **Text** is kept as plain text, without formatting.
- **Images** (screenshots, copied pictures) are shown with a thumbnail.
- **Copied files** from Explorer can be pasted back into a folder.
- Copying something that's already in the list moves it to the top instead of adding a duplicate.
- Copies that the source app marks as private are **not recorded**. Password managers use this to keep passwords out of
  clipboard history.
- History is kept **in memory only**. Nothing is written to disk, and it is empty after a restart or when you exit.

### Tray menu

Right-click the tray icon for:

- **Settings…** (or double-click the icon, or launch the exe again while it's running)
- **Window switching** / **Clipboard history**: turn either feature on or off. Turning clipboard history off also
  clears it.
- **Clear clipboard history**
- **Start with Windows**
- **Exit**

### Settings

- **Keybindings**: click a box and press the shortcut you want. It needs at least one of Ctrl, Alt, Shift or Win.
  <kbd>Backspace</kbd> clears it (the action then has no shortcut). **Reset to defaults** restores the shortcuts above.
- **Window switching**: on/off, and whether to include minimized windows.
- **Clipboard history**: on/off; how many entries to keep (5–30); whether to keep images and copied files; where the
  picker appears; and whether picking an entry pastes it right away or only puts it on the clipboard.
- **General**: start with Windows.

**OK** or **Apply** takes effect immediately. If a shortcut is already used by Windows or another program, or two
actions share the same shortcut, the box is flagged with the reason and nothing is saved.

### Config file

Settings are stored in `%APPDATA%\UnshitWindows\config.json`. You can also edit it by hand while the app is closed:

```json
{
  "nextWindowHotkey": "Alt+OemPipe",
  "previousWindowHotkey": "Alt+Shift+OemPipe",
  "clipboardHotkey": "Ctrl+Shift+V",
  "windowSwitchingEnabled": true,
  "includeMinimizedWindows": true,
  "clipboardHistoryEnabled": true,
  "historySize": 10,
  "keepImages": true,
  "keepFiles": true,
  "popupPosition": "Caret",
  "pasteAfterPick": true
}
```

Shortcuts are modifiers (`Ctrl`, `Alt`, `Shift`, `Win`) plus one key name from .NET's
[`Keys`](https://learn.microsoft.com/dotnet/api/system.windows.forms.keys) enum, such as `V`, `F12`, `D1` or `OemPipe`.
An empty string means no shortcut. `popupPosition` is `Caret` or `Mouse`. Missing fields fall back to their defaults.
If the file can't be read, the app starts with defaults, shows a notification, and keeps the broken file as
`config.json.bak`.

## Known limitations

- Windows doesn't let a normal app control windows running as administrator (elevated terminals, Task Manager). It
  can't paste into them or restore them when they're minimized. Switching to a non-minimized elevated window still
  works. Run UnshitWindows as administrator if you need more.
- With the default shortcut, <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>V</kbd> no longer reaches apps that use it for
  "paste as plain text" (Chrome, Teams). Pick another shortcut in Settings if that matters to you.
- Win-key combinations Windows reserves for itself (<kbd>Win</kbd>+<kbd>V</kbd>, <kbd>Win</kbd>+<kbd>L</kbd>, …) can't
  be used.
- Rich text formatting (HTML/RTF) isn't kept; entries from history paste as plain text.
- A minimized Store (UWP) app, such as Settings or Calculator, can't be matched to its app, so switching won't reach it
  from that app's other windows until it is restored.

## Uninstall

1. Right-click the tray icon, untick **Start with Windows**, then choose **Exit**.
2. Delete the install folder: `%LOCALAPPDATA%\Programs\UnshitWindows`
3. Optionally delete your settings: `%APPDATA%\UnshitWindows`

## Development

```powershell
dotnet build             # build everything
dotnet test              # run the unit tests
dotnet run --project src/UnshitWindows
```

| Path | Contents |
| --- | --- |
| `src/UnshitWindows/TrayContext.cs` | Tray icon and menu; wires hotkeys, settings and features together |
| `src/UnshitWindows/Switching/` | Finding the app's windows and cycling through them |
| `src/UnshitWindows/Clips/` | Clipboard capture, history, the picker and pasting |
| `src/UnshitWindows/Settings/` | Settings window and the shortcut capture box |
| `src/UnshitWindows/Config/` | Shortcut parsing and the JSON config file |
| `src/UnshitWindows/Native/` | Win32 interop |
| `tests/UnshitWindows.Tests/` | xUnit tests for the logic that doesn't need a desktop |
| `tools/publish.ps1` | Builds and installs a Release copy |
| `tools/make-icon.ps1` | Regenerates `app.ico` |

Only one copy runs at a time. Exit the tray copy before running a debug build, otherwise the debug build just opens the
tray copy's Settings and exits.
