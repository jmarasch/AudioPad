using System.Text.Json;
using AudioPad.Core.Models;

namespace AudioPad.Core.Persistence;

/// <summary>Loads and saves <see cref="GridProfile"/> instances as JSON on disk.</summary>
public sealed class ProfileRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>The default profile file path under the user's local application data folder.</summary>
    public static string GetDefaultProfilePath()
    {
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var audioPadDir = Path.Combine(appDataDir, "AudioPad");
        Directory.CreateDirectory(audioPadDir);
        return Path.Combine(audioPadDir, "profile.json");
    }

    /// <summary>Loads a profile from disk, or returns a default 4x4 profile if the file doesn't exist.</summary>
    public GridProfile LoadProfile(string path)
    {
        if (!File.Exists(path))
        {
            return GridProfile.CreateDefault();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GridProfile>(json, SerializerOptions) ?? GridProfile.CreateDefault();
    }

    /// <summary>Saves a profile to disk as indented JSON, creating parent directories as needed.</summary>
    public void SaveProfile(string path, GridProfile profile)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(profile, SerializerOptions);
        File.WriteAllText(path, json);
    }
}
