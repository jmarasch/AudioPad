using AudioPad.Core.Models;

namespace AudioPad.Core.Persistence;

/// <summary>
/// The app's own copy of every clip and icon a pad uses.
///
/// Media is copied in rather than referenced where it sits, on every platform. Referencing in place
/// only ever worked on desktop — Android's file picker returns a <c>content://</c> handle with no
/// usable path — so the two platforms behaved differently for the same action, and a desktop board
/// silently broke the moment its source folders were reorganised. Importing makes a board
/// self-contained and makes both platforms behave alike.
///
/// Each import is its own copy, even of a file already imported elsewhere. That costs disk space
/// for a clip used on several pads, but it means removing one pad can never take the audio out from
/// under another — sharing one file between pads would make deletion a reference-counting problem.
/// See the note on <see cref="Delete"/>.
/// </summary>
public static class MediaLibrary
{
    /// <summary>Subfolder for pad audio.</summary>
    public const string AudioFolder = "audio";

    /// <summary>Subfolder for pad icons.</summary>
    public const string IconFolder = "icons";

    /// <summary>
    /// Copies a picked file into the library and returns its new path.
    ///
    /// Stored under a generated name, keeping only the extension: two clips called "hit.wav" from
    /// different folders are different files, and a name-based scheme has to invent suffixes to
    /// tell them apart. What the user chose is remembered separately, on the pad, and is what gets
    /// shown — see <see cref="Models.PadConfig.AudioFileName"/>.
    /// </summary>
    public static async Task<string> ImportAsync(Stream source, string fileName, string subfolder)
    {
        var directory = AppStorage.GetDirectory(subfolder);
        var destination = Path.Combine(directory, $"{Guid.NewGuid()}{Path.GetExtension(SanitiseFileName(fileName))}");

        await using var target = File.Create(destination);
        await source.CopyToAsync(target);

        return destination;
    }

    /// <summary>
    /// Brings a file that lives outside the library into it, and returns where it now is.
    ///
    /// Boards built before importing existed hold absolute paths into the user's own folders. Those
    /// references are exactly what importing is meant to eliminate: the board breaks if the folder
    /// is reorganised, and the app can't safely delete anything it points at. Adopting the file
    /// copies it in and rewrites the reference, after which the app has no memory of where it came
    /// from.
    ///
    /// A path already in the library is returned untouched, so this is safe to run on every load. A
    /// file that has since gone missing is left as it is rather than dropped, so a broken pad still
    /// says what it was looking for.
    /// </summary>
    public static string? Adopt(string? path, string subfolder)
    {
        if (string.IsNullOrWhiteSpace(path) || IsManaged(path) || !File.Exists(path))
        {
            return path;
        }

        try
        {
            var destination = Path.Combine(
                AppStorage.GetDirectory(subfolder), $"{Guid.NewGuid()}{Path.GetExtension(path)}");
            File.Copy(path, destination);
            return destination;
        }
        catch (Exception)
        {
            // Better to keep pointing at a file that works than to lose the reference entirely.
            return path;
        }
    }

    /// <summary>
    /// Adopts every clip and icon in a setup, returning true if anything moved — which is the
    /// caller's cue to save the rewritten paths.
    /// </summary>
    public static bool AdoptAll(Setup setup)
    {
        var changed = false;

        foreach (var pad in setup.Pages.SelectMany(page => page.Pads))
        {
            var audio = Adopt(pad.AudioFilePath, AudioFolder);
            if (audio != pad.AudioFilePath)
            {
                // The original path is the last chance to learn what this clip was called.
                pad.AudioFileName ??= Path.GetFileName(pad.AudioFilePath);
                pad.AudioFilePath = audio;
                changed = true;
            }

            var icon = Adopt(pad.IconPath, IconFolder);
            if (icon != pad.IconPath)
            {
                pad.IconFileName ??= Path.GetFileName(pad.IconPath);
                pad.IconPath = icon;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Deletes a file this library owns, and does nothing at all for anything else.
    ///
    /// The guard is the point of this method, not an optimisation. Boards created before media was
    /// imported still hold absolute paths into the user's own folders — a pad pointing at a clip in
    /// some project directory — and clearing such a pad must never reach out and delete the
    /// original. Only paths inside the library are ever touched.
    /// </summary>
    public static void Delete(string? path)
    {
        if (!IsManaged(path))
        {
            return;
        }

        try
        {
            File.Delete(path!);
        }
        catch (Exception)
        {
            // A file that's locked or already gone isn't worth failing an edit over.
        }
    }

    /// <summary>Whether a path points inside the library, and is therefore the app's to delete.</summary>
    public static bool IsManaged(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppStorage.GetRootDirectory()));
        var full = Path.GetFullPath(path);

        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>Strips anything that would let a name escape the folder it's being written into.</summary>
    private static string SanitiseFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "clip";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }

}
