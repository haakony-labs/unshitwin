using UnshitWindows.Config;

namespace UnshitWindows.Settings;

/// <summary>Settings dialog. Changes take effect when OK or Apply succeeds.</summary>
internal sealed class SettingsForm : Form
{
    public delegate IReadOnlyDictionary<HotkeyAction, string> ApplyHandler(AppConfig config, bool startWithWindows);

    private readonly ApplyHandler _apply;
    private readonly Action<bool> _setHotkeysSuspended;
    private readonly AppConfig _base;

    private readonly HotkeyBox _nextBox = new() { Width = 200 };
    private readonly HotkeyBox _previousBox = new() { Width = 200 };
    private readonly HotkeyBox _clipboardBox = new() { Width = 200 };

    private readonly CheckBox _switchingEnabled = NewCheck("Turn on same-app window switching");
    private readonly CheckBox _includeMinimized = NewCheck("Include minimized windows", indent: true);

    private readonly CheckBox _clipboardEnabled = NewCheck("Turn on clipboard history");
    private readonly NumericUpDown _historySize = new()
    {
        Minimum = AppConfig.MinHistorySize,
        Maximum = AppConfig.MaxHistorySize,
        Width = 56,
        Anchor = AnchorStyles.Left,
    };
    private readonly CheckBox _keepImages = NewCheck("Keep copied images", indent: true);
    private readonly CheckBox _keepFiles = NewCheck("Keep copied files", indent: true);
    private readonly RadioButton _atCaret = NewRadio("At the text cursor when the app reports one, otherwise at the mouse");
    private readonly RadioButton _atMouse = NewRadio("Always at the mouse pointer");
    private readonly CheckBox _pasteAfterPick = NewCheck("Paste right away when an entry is picked (otherwise only copy it)", indent: true);

    private readonly CheckBox _startWithWindows = NewCheck("Start with Windows");

    private readonly Label _status = new() { AutoSize = true, Margin = new Padding(3, 8, 3, 0) };
    private readonly ErrorProvider _errors;
    private readonly ToolTip _toolTip = new();

    private bool _isActive;
    private bool _hotkeyBoxFocused;

    public SettingsForm(AppConfig config, bool startWithWindows, Icon icon, ApplyHandler apply, Action<bool> setHotkeysSuspended)
    {
        _apply = apply;
        _setHotkeysSuspended = setHotkeysSuspended;
        _base = config.Clone();
        _errors = new ErrorProvider(this) { BlinkStyle = ErrorBlinkStyle.NeverBlink };

        Text = "UnshitWindows Settings";
        Icon = icon;
        Font = SystemFonts.MessageBoxFont;
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        BuildLayout();
        LoadValues(config, startWithWindows);
        WireEvents();
        // Not a shortcut box: focusing one suspends the global hotkeys.
        ActiveControl = _switchingEnabled;
    }

    // ---- layout ----

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = new Point(Padding.Left, Padding.Top),
        };

        // Keybindings
        var keys = NewTable(columns: 2);
        AddHotkeyRow(keys, HotkeyAction.NextWindow, _nextBox);
        AddHotkeyRow(keys, HotkeyAction.PreviousWindow, _previousBox);
        AddHotkeyRow(keys, HotkeyAction.ClipboardPopup, _clipboardBox);
        var hint = new Label
        {
            Text = "Click a box and press the new shortcut. Backspace clears it.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 6, 3, 3),
        };
        keys.Controls.Add(hint);
        keys.SetColumnSpan(hint, 2);
        var reset = new Button { Text = "Reset to defaults", AutoSize = true, Margin = new Padding(3, 6, 3, 3) };
        reset.Click += (_, _) =>
        {
            _nextBox.Value = Defaults.NextWindow;
            _previousBox.Value = Defaults.PreviousWindow;
            _clipboardBox.Value = Defaults.ClipboardPopup;
            _errors.Clear();
        };
        keys.Controls.Add(reset);
        keys.SetColumnSpan(reset, 2);
        root.Controls.Add(NewGroup("Keybindings", keys));

        // Window switching
        var switching = NewStack();
        switching.Controls.AddRange([_switchingEnabled, _includeMinimized]);
        root.Controls.Add(NewGroup("Window switching", switching));

        // Clipboard history
        var clipboard = NewStack();
        var sizeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(20, 3, 3, 3) };
        sizeRow.Controls.Add(new Label { Text = "Entries to keep:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) });
        sizeRow.Controls.Add(_historySize);
        var positionLabel = new Label { Text = "Show the picker:", AutoSize = true, Margin = new Padding(20, 8, 3, 0) };
        clipboard.Controls.AddRange([_clipboardEnabled, sizeRow, _keepImages, _keepFiles, positionLabel, _atCaret, _atMouse, _pasteAfterPick]);
        root.Controls.Add(NewGroup("Clipboard history", clipboard));

        // General
        var general = NewStack();
        general.Controls.Add(_startWithWindows);
        root.Controls.Add(NewGroup("General", general));

        root.Controls.Add(_status);

        // Buttons (right-to-left flow: first added is rightmost)
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 8, 0, 0),
        };
        var apply = new Button { Text = "Apply", AutoSize = true, MinimumSize = new Size(80, 0) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(80, 0) };
        var ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(80, 0) };
        buttons.Controls.AddRange([apply, cancel, ok]);
        root.Controls.Add(buttons);

        apply.Click += (_, _) =>
        {
            if (TryApply()) ShowStatus("Settings saved.", error: false);
        };
        ok.Click += (_, _) =>
        {
            if (TryApply()) Close();
        };
        // The form is modeless, so a button's DialogResult wouldn't close it; Esc maps to this via CancelButton.
        cancel.Click += (_, _) => Close();
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(root);
    }

    private void AddHotkeyRow(TableLayoutPanel table, HotkeyAction action, HotkeyBox box)
    {
        table.Controls.Add(new Label
        {
            Text = HotkeyManager.Describe(action) + ":",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 3, 12, 3),
        });
        box.Margin = new Padding(3, 3, 20, 3); // room for the error icon
        _toolTip.SetToolTip(box, "Click here and press the new shortcut. Backspace clears it.");
        table.Controls.Add(box);
    }

    private static GroupBox NewGroup(string title, Control content)
    {
        var group = new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0, 0, 0, 10),
        };
        content.Dock = DockStyle.Fill;
        group.Controls.Add(content);
        return group;
    }

    private static TableLayoutPanel NewTable(int columns) => new()
    {
        ColumnCount = columns,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
    };

    private static FlowLayoutPanel NewStack() => new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
    };

    private static CheckBox NewCheck(string text, bool indent = false) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(indent ? 20 : 3, 3, 3, 3),
    };

    private static RadioButton NewRadio(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(36, 2, 3, 2),
    };

    // ---- values ----

    private void LoadValues(AppConfig c, bool startWithWindows)
    {
        _nextBox.Value = c.NextWindowHotkey;
        _previousBox.Value = c.PreviousWindowHotkey;
        _clipboardBox.Value = c.ClipboardHotkey;
        _switchingEnabled.Checked = c.WindowSwitchingEnabled;
        _includeMinimized.Checked = c.IncludeMinimizedWindows;
        _clipboardEnabled.Checked = c.ClipboardHistoryEnabled;
        _historySize.Value = Math.Clamp(c.HistorySize, AppConfig.MinHistorySize, AppConfig.MaxHistorySize);
        _keepImages.Checked = c.KeepImages;
        _keepFiles.Checked = c.KeepFiles;
        _atCaret.Checked = c.PopupPosition == PopupPosition.Caret;
        _atMouse.Checked = c.PopupPosition == PopupPosition.Mouse;
        _pasteAfterPick.Checked = c.PasteAfterPick;
        _startWithWindows.Checked = startWithWindows;
        UpdateEnabledState();
    }

    private AppConfig Collect()
    {
        var c = _base.Clone();
        c.NextWindowHotkey = _nextBox.Value;
        c.PreviousWindowHotkey = _previousBox.Value;
        c.ClipboardHotkey = _clipboardBox.Value;
        c.WindowSwitchingEnabled = _switchingEnabled.Checked;
        c.IncludeMinimizedWindows = _includeMinimized.Checked;
        c.ClipboardHistoryEnabled = _clipboardEnabled.Checked;
        c.HistorySize = (int)_historySize.Value;
        c.KeepImages = _keepImages.Checked;
        c.KeepFiles = _keepFiles.Checked;
        c.PopupPosition = _atMouse.Checked ? PopupPosition.Mouse : PopupPosition.Caret;
        c.PasteAfterPick = _pasteAfterPick.Checked;
        return c;
    }

    private bool TryApply()
    {
        _errors.Clear();
        var problems = _apply(Collect(), _startWithWindows.Checked);
        if (problems.Count == 0) return true;

        foreach (var (action, message) in problems) _errors.SetError(BoxFor(action), message);
        var first = problems.First();
        ShowStatus($"Not saved — {HotkeyManager.Describe(first.Key)}: {first.Value}", error: true);
        return false;
    }

    private HotkeyBox BoxFor(HotkeyAction action) => action switch
    {
        HotkeyAction.NextWindow => _nextBox,
        HotkeyAction.PreviousWindow => _previousBox,
        _ => _clipboardBox,
    };

    private void ShowStatus(string text, bool error)
    {
        _status.ForeColor = error ? Color.Firebrick : SystemColors.GrayText;
        _status.Text = text;
    }

    private void UpdateEnabledState()
    {
        _includeMinimized.Enabled = _switchingEnabled.Checked;
        foreach (Control c in new Control[] { _historySize, _keepImages, _keepFiles, _atCaret, _atMouse, _pasteAfterPick })
            c.Enabled = _clipboardEnabled.Checked;
    }

    // ---- hotkey capture ----

    private void WireEvents()
    {
        _switchingEnabled.CheckedChanged += (_, _) => UpdateEnabledState();
        _clipboardEnabled.CheckedChanged += (_, _) => UpdateEnabledState();

        foreach (var box in new[] { _nextBox, _previousBox, _clipboardBox })
        {
            box.Enter += (_, _) => { _hotkeyBoxFocused = true; UpdateSuspension(); };
            box.Leave += (_, _) => { _hotkeyBoxFocused = false; UpdateSuspension(); };
            box.ValueChanged += (s, _) =>
            {
                _errors.SetError((Control)s!, "");
                _status.Text = "";
            };
        }
    }

    // While a shortcut box has focus the global hotkeys are released, otherwise pressing the
    // current shortcut would trigger it instead of reaching the box.
    private void UpdateSuspension() => _setHotkeysSuspended(_isActive && _hotkeyBoxFocused);

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _isActive = true;
        UpdateSuspension();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        _isActive = false;
        UpdateSuspension();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _isActive = false;
        UpdateSuspension();
        _toolTip.Dispose();
        _errors.Dispose();
        base.OnFormClosed(e);
    }
}
