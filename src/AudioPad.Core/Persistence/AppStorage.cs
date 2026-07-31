namespace AudioPad.Core.Persistence;

/// <summary>App-private storage locations under the user's local application data folder.</summary>
public static class AppStorage
{
    /// <summary>The app's private root data directory, creating it if needed.</summary>
    public static string GetRootDirectory()
    {
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(appDataDir, "AudioPad");
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>Returns (creating if needed) a subfolder under the app's private data directory.</summary>
    public static string GetDirectory(string subfolder)
    {
        var directory = Path.Combine(GetRootDirectory(), subfolder);
        Directory.CreateDirectory(directory);
        return directory;
    }
}
