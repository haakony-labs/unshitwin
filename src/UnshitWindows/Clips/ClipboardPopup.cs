using System.Drawing.Drawing2D;
using UnshitWindows.Native;

namespace UnshitWindows.Clips;

/// <summary>Borderless picker listing the clipboard history. Digits 1–9/0 pick directly.</summary>
internal sealed class ClipboardPopup : Form
{
    private const string EmptyText = "Nothing copied yet";
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int CS_DROPSHADOW = 0x00020000;

    private readonly ListBox _list;
    private List<ClipEntry> _entries = [];
    private bool _finishing;
    private Point _cursorAtShow;

    /// <summary>The user picked an entry. Handlers run while the popup is still visible
    /// (so focus can be handed to the target first); it is hidden afterwards.</summary>
    public event Action<ClipEntry>? Picked;

    /// <summary>Esc: the user backed out.</summary>
    public event Action? Cancelled;

    public event Action<ClipEntry>? DeleteRequested;

    public ClipboardPopup()
    {
        Text = "Clipboard history";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Font = SystemFonts.MessageBoxFont;
        Padding = new Padding(1);
        BackColor = SystemColors.ControlDark; // shows through the padding as a 1px border

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawVariable,
            IntegralHeight = false,
            BackColor = SystemColors.Window,
            ForeColor = SystemColors.WindowText,
        };
        _list.MeasureItem += OnMeasureItem;
        _list.DrawItem += OnDrawItem;
        _list.MouseClick += OnListMouseClick;
        _list.MouseMove += OnListMouseMove;
        Controls.Add(_list);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW; // keep it out of Alt+Tab and the taskbar
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    private float UiScale => DeviceDpi / 96f;

    public void ShowAt(IReadOnlyList<ClipEntry> entries, Point anchor)
    {
        _cursorAtShow = Cursor.Position;
        Location = anchor;
        if (!Visible) Show();
        _list.Items.Clear();
        SetEntries(entries);
        PlaceNear(anchor);
        Activate();
        NativeMethods.SetForegroundWindow(Handle);
        _list.Focus();
    }

    /// <summary>Refreshes the rows, keeping the selection where it was when possible.</summary>
    public void SetEntries(IReadOnlyList<ClipEntry> entries)
    {
        int selected = Math.Max(0, _list.SelectedIndex);
        _entries = [.. entries];

        _list.BeginUpdate();
        _list.Items.Clear();
        if (_entries.Count == 0) _list.Items.Add(EmptyText);
        else foreach (var entry in _entries) _list.Items.Add(entry);
        _list.EndUpdate();

        _list.SelectedIndex = _entries.Count == 0 ? -1 : Math.Min(selected, _entries.Count - 1);
        ResizeToContent();
    }

    public void SelectNext()
    {
        if (_entries.Count > 0) _list.SelectedIndex = (_list.SelectedIndex + 1) % _entries.Count;
    }

    private void ResizeToContent()
    {
        int height = 0;
        for (int i = 0; i < _list.Items.Count; i++) height += _list.GetItemHeight(i);
        int maxHeight = (int)(560 * UiScale);
        ClientSize = new Size((int)(460 * UiScale), Math.Min(height, maxHeight) + Padding.Vertical);
    }

    private void PlaceNear(Point anchor)
    {
        var area = Screen.FromPoint(anchor).WorkingArea;
        int x = anchor.X, y = anchor.Y;
        if (x + Width > area.Right) x = area.Right - Width;
        if (x < area.Left) x = area.Left;
        if (y + Height > area.Bottom) y = anchor.Y - Height - (int)(24 * UiScale); // flip above the caret/pointer
        if (y < area.Top) y = area.Top;
        Location = new Point(x, y);
    }

    // ---- choosing ----

    private void ChooseIndex(int index)
    {
        if (index < 0 || index >= _entries.Count) return;
        var entry = _entries[index];
        Finish(() => Picked?.Invoke(entry));
    }

    private void Cancel() => Finish(() => Cancelled?.Invoke());

    private void Finish(Action raise)
    {
        if (_finishing) return;
        _finishing = true;
        try
        {
            raise();
        }
        finally
        {
            if (Visible) Hide();
            _finishing = false;
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // Clicked somewhere else: just go away, without pulling focus back.
        if (!_finishing && Visible) BeginInvoke(Hide);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true; // Alt+F4: hide and keep the form for next time
            Cancel();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        switch (key)
        {
            case Keys.Escape:
                Cancel();
                return true;
            case Keys.Enter:
                ChooseIndex(_list.SelectedIndex);
                return true;
            case Keys.Delete:
                if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _entries.Count)
                    DeleteRequested?.Invoke(_entries[_list.SelectedIndex]);
                return true;
        }

        int digit = key switch
        {
            >= Keys.D0 and <= Keys.D9 => key - Keys.D0,
            >= Keys.NumPad0 and <= Keys.NumPad9 => key - Keys.NumPad0,
            _ => -1,
        };
        if (digit >= 0)
        {
            ChooseIndex(digit == 0 ? 9 : digit - 1);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnListMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) ChooseIndex(_list.IndexFromPoint(e.Location));
    }

    private void OnListMouseMove(object? sender, MouseEventArgs e)
    {
        // The popup can open right under a resting pointer; only real movement should move the selection,
        // otherwise Enter would paste whatever row happened to be under the pointer instead of the newest.
        if (Cursor.Position == _cursorAtShow) return;
        int index = _list.IndexFromPoint(e.Location);
        if (index >= 0 && index < _entries.Count && index != _list.SelectedIndex) _list.SelectedIndex = index;
    }

    // ---- drawing ----

    private void OnMeasureItem(object? sender, MeasureItemEventArgs e)
    {
        bool image = e.Index >= 0 && e.Index < _entries.Count && _entries[e.Index] is ImageEntry;
        e.ItemHeight = image ? (int)(56 * UiScale) : _list.Font.Height + (int)(12 * UiScale);
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var g = e.Graphics;
        var bounds = e.Bounds;
        float s = UiScale;
        const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
                                      TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

        if (_entries.Count == 0)
        {
            g.FillRectangle(SystemBrushes.Window, bounds);
            TextRenderer.DrawText(g, EmptyText, _list.Font, bounds, SystemColors.GrayText, flags | TextFormatFlags.HorizontalCenter);
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color fore = selected ? SystemColors.HighlightText : SystemColors.WindowText;
        Color dim = selected ? SystemColors.HighlightText : SystemColors.GrayText;
        g.FillRectangle(selected ? SystemBrushes.Highlight : SystemBrushes.Window, bounds);

        int pad = (int)(8 * s);
        int labelWidth = (int)(22 * s);
        string label = e.Index < 9 ? (e.Index + 1).ToString() : e.Index == 9 ? "0" : "";
        TextRenderer.DrawText(g, label, _list.Font, new Rectangle(bounds.Left + pad, bounds.Top, labelWidth, bounds.Height), dim, flags);

        int x = bounds.Left + pad + labelWidth;
        var entry = _entries[e.Index];
        if (entry is ImageEntry image)
        {
            int boxWidth = (int)(88 * s);
            int boxHeight = bounds.Height - (int)(8 * s);
            var fit = Fit(image.Thumbnail.Size, boxWidth, boxHeight);
            var dest = new Rectangle(x, bounds.Top + (bounds.Height - fit.Height) / 2, fit.Width, fit.Height);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(image.Thumbnail, dest);
            g.DrawRectangle(SystemPens.ControlDark, dest);
            x += boxWidth + pad;
        }
        TextRenderer.DrawText(g, entry.Preview, _list.Font, new Rectangle(x, bounds.Top, bounds.Right - x - pad, bounds.Height), fore, flags);
    }

    private static Size Fit(Size size, int maxWidth, int maxHeight)
    {
        double scale = Math.Min((double)maxWidth / size.Width, (double)maxHeight / size.Height);
        return new Size(Math.Max(1, (int)(size.Width * scale)), Math.Max(1, (int)(size.Height * scale)));
    }
}
