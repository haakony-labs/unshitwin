using System.Runtime.InteropServices;
using UnshitWindows.Clips;
using UnshitWindows.Config;
using UnshitWindows.Native;
using UnshitWindows.Settings;
using UnshitWindows.Switching;

namespace UnshitWindows;

/// <summary>The app itself: tray icon and menu, hotkeys, and the wiring between the features.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly ConfigStore _store = new(ConfigStore.DefaultPath);
    private readonly MessageWindow _window = new();
    private readonly HotkeyManager _hotkeys;
    private readonly Icon _appIcon;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _switchingItem;
    private readonly ToolStripMenuItem _clipboardItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly SameAppSwitcher _switcher = new();
    private readonly ClipboardHistory _history;
    private readonly ClipboardMonitor _monitor;
    private readonly ClipboardPopup _popup = new();
    private readonly Paster _paster;

    private AppConfig _config;
    private SettingsForm? _settings;
    private AboutForm? _about;
    private nint _pasteTarget;
    private bool _listening;
    private bool _hotkeysSuspended;

    public TrayContext()
    {
        var loaded = _store.Load();
        _config = loaded.Config;

        _hotkeys = new HotkeyManager(_window.Handle);
        _history = new ClipboardHistory(_config.HistorySize);
        _monitor = new ClipboardMonitor(_history, _window.Handle);
        _paster = new Paster(_monitor);
        _appIcon = LoadIcon(null);

        _switchingItem = new ToolStripMenuItem("Window switching", null, (_, _) => ToggleFeature(switching: true));
        _clipboardItem = new ToolStripMenuItem("Clipboard history", null, (_, _) => ToggleFeature(switching: false));
        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup());
        var settingsItem = new ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            settingsItem,
            new ToolStripSeparator(),
            _switchingItem,
            _clipboardItem,
            new ToolStripMenuItem("Clear clipboard history", null, (_, _) => _history.Clear()),
            new ToolStripSeparator(),
            _startupItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("About UnshitWindows", null, (_, _) => OpenAbout()),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()),
        ]);
        settingsItem.Font = new Font(menu.Font, FontStyle.Bold);
        menu.Opening += (_, _) => RefreshMenu();

        _tray = new NotifyIcon
        {
            Icon = LoadIcon(SystemInformation.SmallIconSize),
            Text = "UnshitWindows",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) OpenSettings();
        };

        _window.HotkeyPressed += OnHotkey;
        _window.ClipboardUpdated += _monitor.OnClipboardUpdate;
        _window.ShowSettingsRequested += OpenSettings;
        _popup.Picked += OnPicked;
        _popup.Cancelled += () =>
        {
            if (NativeMethods.IsWindow(_pasteTarget)) WindowActivator.Activate(_pasteTarget);
        };
        _popup.DeleteRequested += entry =>
        {
            _history.Remove(entry);
            _popup.SetEntries(_history.Items);
        };
        Application.ThreadException += OnThreadException;

        ApplyRuntimeSettings();
        var failures = _hotkeys.RegisterAll(_config);

        var problems = new List<string>();
        if (loaded.Error != null) problems.Add(loaded.Error);
        problems.AddRange(failures.Select(f => $"{HotkeyManager.Describe(f.Key)}: {f.Value}"));
        if (problems.Count > 0)
            Notify(failures.Count > 0 ? "Some shortcuts aren't active" : "Settings problem",
                   string.Join("\n", problems) + (failures.Count > 0 ? "\nChange them in Settings." : ""),
                   ToolTipIcon.Warning);
    }

    // ---- hotkeys ----

    private void OnHotkey(int id)
    {
        switch ((HotkeyAction)id)
        {
            case HotkeyAction.NextWindow:
                _switcher.OnHotkey(forward: true, _config.NextWindowHotkey.Modifiers);
                break;
            case HotkeyAction.PreviousWindow:
                _switcher.OnHotkey(forward: false, _config.PreviousWindowHotkey.Modifiers);
                break;
            case HotkeyAction.ClipboardPopup:
                ShowClipboardPopup();
                break;
        }
    }

    private void SetHotkeysSuspended(bool suspended)
    {
        if (suspended == _hotkeysSuspended) return;
        _hotkeysSuspended = suspended;
        if (suspended) _hotkeys.UnregisterAll();
        else _hotkeys.RegisterAll(_config);
    }

    // ---- clipboard ----

    private void ShowClipboardPopup()
    {
        if (!_config.ClipboardHistoryEnabled) return;
        if (_popup.Visible)
        {
            _popup.SelectNext(); // pressing the shortcut again steps down the list
            return;
        }
        _pasteTarget = NativeMethods.GetForegroundWindow();
        _popup.ShowAt(_history.Items, GetPopupAnchor(_pasteTarget));
    }

    private Point GetPopupAnchor(nint target)
    {
        if (_config.PopupPosition == PopupPosition.Caret && target != 0)
        {
            uint thread = NativeMethods.GetWindowThreadProcessId(target, out _);
            var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
            if (NativeMethods.GetGUIThreadInfo(thread, ref info) && info.hwndCaret != 0
                && (info.flags & NativeMethods.GUI_CARETBLINKING) != 0)
            {
                var point = new POINT { X = info.rcCaret.Left, Y = info.rcCaret.Bottom };
                if (NativeMethods.ClientToScreen(info.hwndCaret, ref point))
                    return new Point(point.X, point.Y + 2);
            }
        }
        return Cursor.Position;
    }

    private async void OnPicked(ClipEntry entry)
    {
        _history.MoveToTop(entry);
        try
        {
            await _paster.PasteAsync(entry, _pasteTarget, _config.PasteAfterPick);
        }
        catch (ExternalException ex)
        {
            Notify("Couldn't paste", $"The clipboard is busy ({ex.Message}). Try again.", ToolTipIcon.Warning);
        }
    }

    private void SetClipboardListening(bool listen)
    {
        if (listen == _listening) return;
        if (listen)
        {
            _listening = NativeMethods.AddClipboardFormatListener(_window.Handle);
        }
        else
        {
            NativeMethods.RemoveClipboardFormatListener(_window.Handle);
            _listening = false;
        }
    }

    // ---- settings ----

    /// <summary>Pushes the non-hotkey parts of <see cref="_config"/> into the running features.</summary>
    private void ApplyRuntimeSettings()
    {
        _switcher.IncludeMinimized = _config.IncludeMinimizedWindows;
        if (!_config.WindowSwitchingEnabled) _switcher.EndSession();

        _history.Capacity = _config.HistorySize;
        _monitor.KeepImages = _config.KeepImages;
        _monitor.KeepFiles = _config.KeepFiles;
        SetClipboardListening(_config.ClipboardHistoryEnabled);
        if (!_config.ClipboardHistoryEnabled)
        {
            _history.Clear(); // turning it off shouldn't leave old copies lying around in memory
            if (_popup.Visible) _popup.Hide();
        }
    }

    private IReadOnlyDictionary<HotkeyAction, string> ApplySettings(AppConfig next, bool startWithWindows)
    {
        next.Normalize();
        var problems = _hotkeys.TryApply(next, _config);
        if (problems.Count > 0)
        {
            if (_hotkeysSuspended) _hotkeys.UnregisterAll();
            return problems;
        }
        if (_hotkeysSuspended) _hotkeys.UnregisterAll();

        _config = next;
        Save();
        ApplyRuntimeSettings();
        try
        {
            if (startWithWindows != StartupRegistration.IsEnabled()) StartupRegistration.SetEnabled(startWithWindows);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            Notify("Couldn't change \"Start with Windows\"", ex.Message, ToolTipIcon.Warning);
        }
        return problems;
    }

    private void Save()
    {
        try
        {
            _store.Save(_config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notify("Couldn't save settings", ex.Message, ToolTipIcon.Error);
        }
    }

    private void OpenSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            if (_settings.WindowState == FormWindowState.Minimized) _settings.WindowState = FormWindowState.Normal;
            _settings.Activate();
            return;
        }
        _settings = new SettingsForm(_config, StartupRegistration.IsEnabled(), _appIcon, ApplySettings, SetHotkeysSuspended);
        _settings.FormClosed += (_, _) =>
        {
            _settings = null;
            SetHotkeysSuspended(false);
        };
        _settings.Show();
        _settings.Activate();
    }

    private void OpenAbout()
    {
        if (_about is { IsDisposed: false })
        {
            _about.Activate();
            return;
        }
        _about = new AboutForm(_appIcon);
        _about.FormClosed += (_, _) => _about = null;
        _about.Show();
        _about.Activate();
    }

    // ---- tray menu ----

    private void RefreshMenu()
    {
        _switchingItem.Checked = _config.WindowSwitchingEnabled;
        _clipboardItem.Checked = _config.ClipboardHistoryEnabled;
        // The Settings window holds its own copy of these; avoid two sources of truth while it's open.
        _switchingItem.Enabled = _clipboardItem.Enabled = _settings == null;
        _startupItem.Checked = StartupRegistration.IsEnabled();
    }

    private void ToggleFeature(bool switching)
    {
        var next = _config.Clone();
        if (switching) next.WindowSwitchingEnabled = !next.WindowSwitchingEnabled;
        else next.ClipboardHistoryEnabled = !next.ClipboardHistoryEnabled;

        var problems = _hotkeys.TryApply(next, _config);
        if (problems.Count > 0)
        {
            Notify("Couldn't turn it on",
                   string.Join("\n", problems.Select(p => $"{HotkeyManager.Describe(p.Key)}: {p.Value}")),
                   ToolTipIcon.Warning);
            return;
        }
        _config = next;
        Save();
        ApplyRuntimeSettings();
    }

    private void ToggleStartup()
    {
        try
        {
            StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            Notify("Couldn't change \"Start with Windows\"", ex.Message, ToolTipIcon.Warning);
        }
    }

    // ---- misc ----

    private void Notify(string title, string text, ToolTipIcon icon) =>
        _tray.ShowBalloonTip(8000, title, text, icon);

    private void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.DefaultDirectory);
            File.AppendAllText(Path.Combine(ConfigStore.DefaultDirectory, "error.log"),
                               $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n\n");
        }
        catch (Exception logEx) when (logEx is IOException or UnauthorizedAccessException)
        {
            // nothing more we can do
        }
        Notify("Something went wrong", e.Exception.Message + "\nDetails are in error.log next to config.json.", ToolTipIcon.Error);
    }

    private static Icon LoadIcon(Size? size)
    {
        using var stream = typeof(TrayContext).Assembly.GetManifestResourceStream("UnshitWindows.app.ico")
                           ?? throw new InvalidOperationException("The app icon resource is missing.");
        return size is { } s ? new Icon(stream, s) : new Icon(stream);
    }

    protected override void ExitThreadCore()
    {
        _settings?.Close();
        _about?.Close();
        Application.ThreadException -= OnThreadException;
        _switcher.Dispose();
        _hotkeys.UnregisterAll();
        SetClipboardListening(false);
        _popup.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _window.Dispose();
        base.ExitThreadCore();
    }
}
