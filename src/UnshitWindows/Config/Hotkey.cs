using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnshitWindows.Native;

namespace UnshitWindows.Config;

/// <summary>Values match the Win32 MOD_* flags so they can be passed to RegisterHotKey as-is.</summary>
[Flags]
internal enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>A global shortcut: modifiers plus one virtual key. <c>default</c> means "not bound".</summary>
[JsonConverter(typeof(HotkeyJsonConverter))]
internal readonly record struct Hotkey(HotkeyModifiers Modifiers, Keys Key)
{
    private static readonly (HotkeyModifiers Flag, string Name)[] ModifierOrder =
    [
        (HotkeyModifiers.Control, "Ctrl"),
        (HotkeyModifiers.Alt, "Alt"),
        (HotkeyModifiers.Shift, "Shift"),
        (HotkeyModifiers.Win, "Win"),
    ];

    public static Hotkey None => default;

    public bool IsEmpty => Key == Keys.None;

    /// <summary>Storage form, e.g. <c>Ctrl+Shift+V</c>. Empty string when unbound.</summary>
    public override string ToString()
    {
        if (IsEmpty) return "";
        var sb = new StringBuilder();
        foreach (var (flag, name) in ModifierOrder)
            if ((Modifiers & flag) != 0) sb.Append(name).Append('+');
        sb.Append(Key.ToString());
        return sb.ToString();
    }

    /// <summary>Parses the storage form. Empty or whitespace parses to <see cref="None"/>.</summary>
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = None;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (text.Contains(',')) return false;

        string[] parts = text.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = HotkeyModifiers.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var modifier = ParseModifier(parts[i]);
            if (modifier == HotkeyModifiers.None) return false;
            modifiers |= modifier;
        }

        string keyText = parts[^1];
        if (keyText.Length == 0 || ParseModifier(keyText) != HotkeyModifiers.None) return false;
        if (keyText.All(char.IsAsciiDigit)) return false; // "5" would otherwise parse as a raw enum value
        if (!Enum.TryParse(keyText, ignoreCase: true, out Keys key)) return false;
        if (key == Keys.None || (key & ~Keys.KeyCode) != 0 || IsModifierKey(key)) return false;

        hotkey = new Hotkey(modifiers, key);
        return true;
    }

    /// <summary>Human-readable form using the current keyboard layout, e.g. <c>Alt + |</c>.</summary>
    public string ToDisplayString() =>
        IsEmpty ? "None" : JoinDisplay(Modifiers, KeyDisplayName(Key));

    public static string FormatModifiers(HotkeyModifiers modifiers) => JoinDisplay(modifiers, null);

    public static bool IsModifierKey(Keys key) => key is
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.Menu or Keys.LMenu or Keys.RMenu or
        Keys.LWin or Keys.RWin;

    internal static string KeyDisplayName(Keys key)
    {
        if (key is >= Keys.A and <= Keys.Z) return ((char)key).ToString();
        if (key is >= Keys.D0 and <= Keys.D9) return ((char)('0' + (key - Keys.D0))).ToString();
        if (key is >= Keys.OemSemicolon and <= Keys.Oem102)
        {
            // Ask the active layout which character this key produces (e.g. VK_OEM_5 is '|' on Norwegian).
            uint mapped = NativeMethods.MapVirtualKeyEx((uint)key, NativeMethods.MAPVK_VK_TO_CHAR, NativeMethods.GetKeyboardLayout(0));
            char c = (char)(mapped & 0xFFFF);
            if (c != 0 && !char.IsWhiteSpace(c)) return char.ToUpperInvariant(c).ToString();
        }
        return key switch
        {
            Keys.Space => "Space",
            Keys.Return => "Enter",
            Keys.Back => "Backspace",
            Keys.Escape => "Esc",
            Keys.PageUp => "Page Up",
            Keys.PageDown => "Page Down",
            Keys.Insert => "Insert",
            Keys.Delete => "Delete",
            Keys.Capital => "Caps Lock",
            Keys.Snapshot => "Print Screen",
            Keys.Apps => "Menu",
            >= Keys.NumPad0 and <= Keys.NumPad9 => "Num " + (key - Keys.NumPad0),
            _ => key.ToString(),
        };
    }

    private static string JoinDisplay(HotkeyModifiers modifiers, string? key)
    {
        var parts = new List<string>(5);
        foreach (var (flag, name) in ModifierOrder)
            if ((modifiers & flag) != 0) parts.Add(name);
        if (key != null) parts.Add(key);
        return string.Join(" + ", parts);
    }

    private static HotkeyModifiers ParseModifier(string text) => text.ToLowerInvariant() switch
    {
        "ctrl" or "control" => HotkeyModifiers.Control,
        "alt" => HotkeyModifiers.Alt,
        "shift" => HotkeyModifiers.Shift,
        "win" or "windows" => HotkeyModifiers.Win,
        _ => HotkeyModifiers.None,
    };
}

internal sealed class HotkeyJsonConverter : JsonConverter<Hotkey>
{
    public override bool HandleNull => true;

    public override Hotkey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return Hotkey.None;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("A shortcut must be a string such as \"Ctrl+Shift+V\".");
        string? text = reader.GetString();
        return Hotkey.TryParse(text, out var hotkey)
            ? hotkey
            : throw new JsonException($"\"{text}\" is not a valid shortcut.");
    }

    public override void Write(Utf8JsonWriter writer, Hotkey value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
