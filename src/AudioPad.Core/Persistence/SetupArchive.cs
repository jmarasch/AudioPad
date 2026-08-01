using System.IO.Compression;
using System.Text.Json;
using AudioPad.Core.Models;

namespace AudioPad.Core.Persistence;

/// <summary>
/// Exports/imports a <see cref="Page"/> or <see cref="Setup"/> as a self-contained zip archive,
/// bundling copies of every referenced audio/icon file so the result is portable to a different
/// machine, not just a different path on the same one. Stream-based rather than path-based: on
/// Android, both the export destination and import source may be a Storage-Access-Framework
/// `content://` handle rather than a plain file, so the UI layer always drives this through
/// <c>IStorageFile.OpenWriteAsync()</c>/<c>OpenReadAsync()</c>.
/// </summary>
public static class SetupArchive
{
    private const string PageEntryName = "page.json";
    private const string SetupEntryName = "setup.json";
    private const string MediaEntryPrefix = "media/";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static async Task ExportPageAsync(Stream destination, Page page)
    {
        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var exportable = ClonePage(page);
        await BundleMediaAsync(zip, exportable.Pads, new Dictionary<string, string>());
        await WriteJsonEntryAsync(zip, PageEntryName, exportable);
    }

    public static async Task ExportSetupAsync(Stream destination, Setup setup)
    {
        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var exportable = CloneSetup(setup);
        var mediaNames = new Dictionary<string, string>();
        foreach (var page in exportable.Pages)
        {
            await BundleMediaAsync(zip, page.Pads, mediaNames);
        }

        await WriteJsonEntryAsync(zip, SetupEntryName, exportable);
    }

    /// <summary>
    /// Imports an archive of either kind, reporting which one it held. This is what the UI calls:
    /// the user picks a file, not a file <em>type</em>, so what it contains has to be discovered
    /// rather than assumed.
    /// </summary>
    /// <exception cref="InvalidDataException">The stream isn't an AudioPad archive.</exception>
    public static async Task<ArchiveContents> ImportAsync(Stream source)
    {
        // Reading a zip requires seeking, and a stream handed back by Android's Storage Access
        // Framework generally can't, so it's buffered first. Exports don't need this: writing a
        // zip only appends.
        if (source.CanSeek)
        {
            return await ReadContentsAsync(source);
        }

        await using var buffered = new MemoryStream();
        await source.CopyToAsync(buffered);
        buffered.Position = 0;
        return await ReadContentsAsync(buffered);
    }

    /// <summary>Imports an archive expected to hold a single page.</summary>
    /// <exception cref="InvalidDataException">The archive held a whole setup instead.</exception>
    public static async Task<Page> ImportPageAsync(Stream source) =>
        (await ImportAsync(source)).Page
        ?? throw new InvalidDataException("This archive holds a whole setup, not a single page.");

    /// <summary>Imports an archive expected to hold a whole setup.</summary>
    /// <exception cref="InvalidDataException">The archive held a single page instead.</exception>
    public static async Task<Setup> ImportSetupAsync(Stream source) =>
        (await ImportAsync(source)).Setup
        ?? throw new InvalidDataException("This archive holds a single page, not a whole setup.");

    private static async Task<ArchiveContents> ReadContentsAsync(Stream source)
    {
        using var zip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        if (zip.GetEntry(SetupEntryName) is not null)
        {
            var setup = await ReadJsonEntryAsync<Setup>(zip, SetupEntryName);
            var resolved = new Dictionary<string, string>();
            foreach (var page in setup.Pages)
            {
                await ResolveMediaAsync(zip, page.Pads, resolved);
            }

            return new ArchiveContents(null, setup);
        }

        if (zip.GetEntry(PageEntryName) is not null)
        {
            var page = await ReadJsonEntryAsync<Page>(zip, PageEntryName);
            await ResolveMediaAsync(zip, page.Pads, new Dictionary<string, string>());
            return new ArchiveContents(page, null);
        }

        throw new InvalidDataException("This file isn't an AudioPad export — it holds no page or setup.");
    }

    private static async Task BundleMediaAsync(ZipArchive zip, List<PadConfig> pads, Dictionary<string, string> mediaNames)
    {
        foreach (var pad in pads)
        {
            pad.AudioFilePath = await BundleFileAsync(zip, pad.AudioFilePath, mediaNames);
            pad.IconPath = await BundleFileAsync(zip, pad.IconPath, mediaNames);
        }
    }

    /// <summary>Copies a referenced file into the archive (once per distinct source path) and
    /// returns its entry path, or null if there was nothing to bundle.</summary>
    private static async Task<string?> BundleFileAsync(ZipArchive zip, string? sourcePath, Dictionary<string, string> mediaNames)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        if (mediaNames.TryGetValue(sourcePath, out var entryName))
        {
            return MediaEntryPrefix + entryName;
        }

        entryName = MakeUniqueEntryName(Path.GetFileName(sourcePath), mediaNames.Values);
        mediaNames[sourcePath] = entryName;

        var entry = zip.CreateEntry(MediaEntryPrefix + entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await using var sourceStream = File.OpenRead(sourcePath);
        await sourceStream.CopyToAsync(entryStream);

        return MediaEntryPrefix + entryName;
    }

    private static string MakeUniqueEntryName(string fileName, IEnumerable<string> existingNames)
    {
        var existing = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        if (existing.Add(fileName))
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        for (var i = 1; ; i++)
        {
            var candidate = $"{stem} ({i}){extension}";
            if (existing.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static async Task ResolveMediaAsync(ZipArchive zip, List<PadConfig> pads, Dictionary<string, string> resolved)
    {
        foreach (var pad in pads)
        {
            pad.AudioFilePath = await ResolveFileAsync(zip, pad.AudioFilePath, "audio", resolved);
            pad.IconPath = await ResolveFileAsync(zip, pad.IconPath, "icons", resolved);
        }
    }

    /// <summary>Extracts a referenced media entry (once per distinct entry path) into app-private
    /// storage and returns its new absolute path, or null if there was nothing to resolve.</summary>
    private static async Task<string?> ResolveFileAsync(ZipArchive zip, string? entryPath, string subfolder, Dictionary<string, string> resolved)
    {
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            return null;
        }

        if (resolved.TryGetValue(entryPath, out var destination))
        {
            return destination;
        }

        var entry = zip.GetEntry(entryPath);
        if (entry is null)
        {
            return null;
        }

        // Keeps the clip's own name rather than renaming it to a GUID, so what lands in the media
        // library is still recognisable as the file that was originally chosen.
        await using var entryStream = entry.Open();
        var destinationPath = await MediaLibrary.ImportAsync(entryStream, entry.Name, subfolder);

        resolved[entryPath] = destinationPath;
        return destinationPath;
    }

    private static async Task WriteJsonEntryAsync<T>(ZipArchive zip, string entryName, T value)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, value, SerializerOptions);
    }

    private static async Task<T> ReadJsonEntryAsync<T>(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName) ?? throw new InvalidDataException($"Archive is missing '{entryName}'.");
        await using var entryStream = entry.Open();
        return await JsonSerializer.DeserializeAsync<T>(entryStream, SerializerOptions)
            ?? throw new InvalidDataException($"Archive's '{entryName}' entry is empty or invalid.");
    }

    private static Page ClonePage(Page page) => JsonSerializer.Deserialize<Page>(JsonSerializer.Serialize(page))!;

    private static Setup CloneSetup(Setup setup) => JsonSerializer.Deserialize<Setup>(JsonSerializer.Serialize(setup))!;
}
