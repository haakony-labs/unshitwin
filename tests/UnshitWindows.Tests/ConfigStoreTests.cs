using UnshitWindows.Config;

namespace UnshitWindows.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "UnshitWindowsTests-" + Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(_dir, "config.json");

    public ConfigStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void MissingFile_GivesDefaults()
    {
        var result = new ConfigStore(Path.Combine(_dir, "nope", "config.json")).Load();
        Assert.Null(result.Error);
        Assert.Equal(Defaults.ClipboardPopup, result.Config.ClipboardHotkey);
        Assert.Equal(10, result.Config.HistorySize);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new ConfigStore(Path.Combine(_dir, "sub", "config.json")); // directory is created on save
        var config = new AppConfig
        {
            NextWindowHotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.N),
            PreviousWindowHotkey = Hotkey.None,
            ClipboardHotkey = new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Shift, Keys.C),
            WindowSwitchingEnabled = false,
            IncludeMinimizedWindows = false,
            ClipboardHistoryEnabled = false,
            HistorySize = 25,
            KeepImages = false,
            KeepFiles = false,
            PopupPosition = PopupPosition.Mouse,
            PasteAfterPick = false,
        };

        store.Save(config);
        var loaded = store.Load();

        Assert.Null(loaded.Error);
        var c = loaded.Config;
        Assert.Equal(config.NextWindowHotkey, c.NextWindowHotkey);
        Assert.True(c.PreviousWindowHotkey.IsEmpty);
        Assert.Equal(config.ClipboardHotkey, c.ClipboardHotkey);
        Assert.False(c.WindowSwitchingEnabled);
        Assert.False(c.IncludeMinimizedWindows);
        Assert.False(c.ClipboardHistoryEnabled);
        Assert.Equal(25, c.HistorySize);
        Assert.False(c.KeepImages);
        Assert.False(c.KeepFiles);
        Assert.Equal(PopupPosition.Mouse, c.PopupPosition);
        Assert.False(c.PasteAfterPick);
    }

    [Fact]
    public void SavedFile_IsReadable()
    {
        var store = new ConfigStore(ConfigPath);
        store.Save(new AppConfig { ClipboardHotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.V) });
        string json = File.ReadAllText(ConfigPath);
        Assert.Contains("\"clipboardHotkey\": \"Ctrl+Shift+V\"", json);
        Assert.Contains("\"popupPosition\": \"Caret\"", json);
    }

    [Fact]
    public void PartialFile_KeepsDefaultsForMissingFields_AndIgnoresUnknownOnes()
    {
        File.WriteAllText(ConfigPath, """
            {
              // comments and trailing commas are fine
              "historySize": 15,
              "somethingFromTheFuture": true,
            }
            """);

        var result = new ConfigStore(ConfigPath).Load();

        Assert.Null(result.Error);
        Assert.Equal(15, result.Config.HistorySize);
        Assert.Equal(Defaults.NextWindow, result.Config.NextWindowHotkey);
        Assert.True(result.Config.ClipboardHistoryEnabled);
    }

    [Theory]
    [InlineData(1, AppConfig.MinHistorySize)]
    [InlineData(1000, AppConfig.MaxHistorySize)]
    public void HistorySize_IsClamped(int stored, int expected)
    {
        File.WriteAllText(ConfigPath, $$"""{ "historySize": {{stored}} }""");
        Assert.Equal(expected, new ConfigStore(ConfigPath).Load().Config.HistorySize);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{ "clipboardHotkey": "Ctrl+Banana" }""")]
    public void CorruptFile_GivesDefaults_ReportsError_AndKeepsBackup(string content)
    {
        File.WriteAllText(ConfigPath, content);

        var result = new ConfigStore(ConfigPath).Load();

        Assert.NotNull(result.Error);
        Assert.Equal(Defaults.ClipboardPopup, result.Config.ClipboardHotkey);
        Assert.Equal(content, File.ReadAllText(ConfigPath + ".bak"));
    }
}
