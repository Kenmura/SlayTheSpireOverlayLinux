using System.Text.Json;

namespace SlayTheSpireMod.Common;

/// <summary>Loads and atomically saves a mod's JSON config. A missing or corrupt file yields defaults.</summary>
public static class JsonConfigStore
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    public static T Load<T>(string path)
        where T : new()
    {
        try
        {
            if (!File.Exists(path)) return new T();
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new T();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new T();
        }
    }

    /// <summary>Writes via a temp file then moves it into place. Returns false (and reports) on failure.</summary>
    public static bool Save<T>(string path, T value, Action<string>? onError = null)
    {
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, IndentedOptions));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            onError?.Invoke($"Error saving {Path.GetFileName(path)}: {ex.Message}");
            return false;
        }
    }
}
