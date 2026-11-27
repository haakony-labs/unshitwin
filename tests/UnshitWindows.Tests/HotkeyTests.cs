using UnshitWindows.Config;

namespace UnshitWindows.Tests;

public class HotkeyTests
{
    [Theory]
    // Modifiers are passed as int: HotkeyModifiers is internal and can't appear in a public test signature.
    [InlineData("Ctrl+Shift+V", (int)(HotkeyModifiers.Control | HotkeyModifiers.Shift), Keys.V)]
    [InlineData("alt+oem5", (int)HotkeyModifiers.Alt, Keys.Oem5)]
    [InlineData(" Win + Alt + F12 ", (int)(HotkeyModifiers.Win | HotkeyModifiers.Alt), Keys.F12)]
    [InlineData("Control+D1", (int)HotkeyModifiers.Control, Keys.D1)]
    [InlineData("F13", (int)HotkeyModifiers.None, Keys.F13)]
    public void TryParse_Valid(string text, int modifiers, Keys key)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(new Hotkey((HotkeyModifiers)modifiers, key), hotkey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_EmptyMeansUnbound(string? text)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.True(hotkey.IsEmpty);
    }

    [Theory]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Hyper+V")]
    [InlineData("Ctrl+NotAKey")]
    [InlineData("Ctrl+5")]
    [InlineData("Ctrl+A, B")]
    [InlineData("Ctrl+ShiftKey")]
    [InlineData("Ctrl+V+X")]
    public void TryParse_Invalid(string text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Theory]
    [InlineData((int)(HotkeyModifiers.Control | HotkeyModifiers.Shift), Keys.V)]
    [InlineData((int)HotkeyModifiers.Alt, Keys.Oem5)]
    [InlineData((int)(HotkeyModifiers.Alt | HotkeyModifiers.Shift), Keys.Oem3)]
    [InlineData((int)(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win), Keys.Delete)]
    [InlineData((int)HotkeyModifiers.Control, Keys.NumPad7)]
    public void ToString_RoundTrips(int modifiers, Keys key)
    {
        var original = new Hotkey((HotkeyModifiers)modifiers, key);
        Assert.True(Hotkey.TryParse(original.ToString(), out var parsed));
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void ToString_UsesFixedModifierOrder()
    {
        var hotkey = new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Control, Keys.K);
        Assert.Equal("Ctrl+Alt+Shift+Win+K", hotkey.ToString());
    }

    [Fact]
    public void Unbound_ToStringIsEmpty_AndDisplaysAsNone()
    {
        Assert.Equal("", Hotkey.None.ToString());
        Assert.Equal("None", Hotkey.None.ToDisplayString());
    }

    [Fact]
    public void DisplayString_UsesFriendlyNames()
    {
        Assert.Equal("Ctrl + Shift + V", new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.V).ToDisplayString());
        Assert.Equal("Alt + 1", new Hotkey(HotkeyModifiers.Alt, Keys.D1).ToDisplayString());
        Assert.Equal("Ctrl + Page Down", new Hotkey(HotkeyModifiers.Control, Keys.PageDown).ToDisplayString());
    }

    [Fact]
    public void DefaultSwitchKeyIsTheKeyAboveTab()
    {
        // Resolved from scan code 0x29, so it's an OEM key on every common layout.
        Assert.InRange((int)Defaults.AboveTabKey, (int)Keys.OemSemicolon, (int)Keys.Oem102);
        Assert.Equal(HotkeyModifiers.Alt, Defaults.NextWindow.Modifiers);
        Assert.Equal(HotkeyModifiers.Alt | HotkeyModifiers.Shift, Defaults.PreviousWindow.Modifiers);
    }
}
