using System.Text.Json;
using AudioPad.Core.Models;

namespace AudioPad.Core.Persistence;

/// <summary>Loads and saves the whole <see cref="Setup"/> (all pages) as JSON on disk.</summary>
public sealed class SetupRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>The default setup file path under the user's local application data folder.</summary>
    public static string GetDefaultSetupPath() => Path.Combine(AppStorage.GetRootDirectory(), "setup.json");

    /// <summary>Loads a setup from disk, or returns a default single-page setup if the file doesn't exist.</summary>
    public Setup LoadSetup(string path)
    {
        if (!File.Exists(path))
        {
            return Setup.CreateDefault();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Setup>(json, SerializerOptions) ?? Setup.CreateDefault();
    }

    /// <summary>Saves a setup to disk as indented JSON, creating parent directories as needed.</summary>
    public void SaveSetup(string path, Setup setup)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(setup, SerializerOptions);
        File.WriteAllText(path, json);
    }
}
