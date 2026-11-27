using UnshitWindows.Config;
using UnshitWindows.Native;

namespace UnshitWindows.Settings;

/// <summary>Read-only text box that records the next key combination pressed in it.</summary>
internal sealed class HotkeyBox : TextBox
{
    private Hotkey _value;
    private bool _showingTransient;

    public event EventHandler? ValueChanged;

    public HotkeyBox()
    {
        ReadOnly = true;
        BackColor = SystemColors.Window;
        ShortcutsEnabled = false;
        Cursor = Cursors.Hand;
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Hotkey Value
    {
        get => _value;
        set
        {
            _value = value;
            ShowValue();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (msg.Msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN && HandleKey(keyData)) return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool HandleKey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        var modifiers = HotkeyModifiers.None;
        if ((keyData & Keys.Control) != 0) modifiers |= HotkeyModifiers.Control;
        if ((keyData & Keys.Alt) != 0) modifiers |= HotkeyModifiers.Alt;
        if ((keyData & Keys.Shift) != 0) modifiers |= HotkeyModifiers.Shift;
        if (Input.AnyDown(NativeMethods.VK_LWIN, NativeMethods.VK_RWIN)) modifiers |= HotkeyModifiers.Win;

        // Unassigned/IME keys that tools inject (AutoHotkey's vkE8, vkFF, IME processing keys): not a real press.
        if (key is Keys.None or Keys.ProcessKey or Keys.Packet || (int)key is 0xE8 or 0xFF) return true;

        if (Hotkey.IsModifierKey(key))
        {
            ShowTransient(Hotkey.FormatModifiers(modifiers) + " + …");
            return true;
        }

        if (modifiers == HotkeyModifiers.None)
        {
            switch (key)
            {
                case Keys.Tab or Keys.Escape:
                    return false; // keep normal dialog navigation / cancel
                case Keys.Back or Keys.Delete:
                    SetValue(Hotkey.None);
                    return true;
                default:
                    ShowTransient("Add Ctrl, Alt, Shift or Win");
                    return true;
            }
        }
        if (modifiers == HotkeyModifiers.Shift && key == Keys.Tab) return false;

        SetValue(new Hotkey(modifiers, key));
        return true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        // Released the modifiers without pressing a key: show the stored value again.
        if (_showingTransient && ModifierKeys == Keys.None && !Input.AnyDown(NativeMethods.VK_LWIN, NativeMethods.VK_RWIN)
            && Text.EndsWith('…'))
            ShowValue();
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        ShowValue();
    }

    private void SetValue(Hotkey value)
    {
        Value = value;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowTransient(string text)
    {
        _showingTransient = true;
        Text = text;
    }

    private void ShowValue()
    {
        _showingTransient = false;
        Text = _value.ToDisplayString();
    }
}
