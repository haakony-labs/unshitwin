using System.Text.Encodings.Web;
using System.Text.Json;

namespace UnshitWindows.Config;

internal sealed record ConfigLoadResult(AppConfig Config, string? Error);

/// <summary>Reads and writes the JSON config file.</summary>
internal sealed class ConfigStore(string filePath)
{
    // The default encoder writes '+' as +, which makes "Ctrl+Shift+V" unreadable for hand-editing.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string FilePath { get; } = filePath;

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnshitWindows");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "config.json");

    /// <summary>Missing file gives defaults. A file that can't be read also gives defaults,
    /// plus an error message; the bad file is copied to <c>.bak</c> so it isn't lost on the next save.</summary>
    public ConfigLoadResult Load()
    {
        if (!File.Exists(FilePath)) return new(new AppConfig(), null);
        try
        {
            string json = File.ReadAllText(FilePath);
            var config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig)
                         ?? throw new JsonException("The file contains no settings.");
            config.Normalize();
            return new(config, null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            string backup = FilePath + ".bak";
            try
            {
                File.Copy(FilePath, backup, overwrite: true);
            }
            catch (Exception copyEx) when (copyEx is IOException or UnauthorizedAccessException)
            {
                backup = "(could not make a backup copy)";
            }
            return new(new AppConfig(), $"Couldn't read {FilePath}: {ex.Message} Using default settings; the old file was kept as {backup}.");
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        using (var stream = File.Create(temp))
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
            JsonSerializer.Serialize(writer, config, ConfigJsonContext.Default.AppConfig);
        File.Move(temp, FilePath, overwrite: true);
    }
}
