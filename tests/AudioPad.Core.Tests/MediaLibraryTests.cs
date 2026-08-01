using AudioPad.Core.Persistence;
using Models = AudioPad.Core.Models;

namespace AudioPad.Core.Tests;

public class MediaLibraryTests : IDisposable
{
    private readonly List<string> _cleanup = [];

    /// <summary>Stored under a generated name, keeping the extension so playback can still
    /// identify the format. The name shown to the user is kept on the pad instead.</summary>
    [Fact]
    public async Task Import_StoresUnderAGeneratedNameAndKeepsTheExtension()
    {
        var path = await ImportAsync("cheering.mp3", [1, 2, 3]);

        Assert.Equal(".mp3", Path.GetExtension(path));
        Assert.NotEqual("cheering.mp3", Path.GetFileName(path));
        Assert.Equal<byte[]>([1, 2, 3], await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Import_NeverOverwritesAnEarlierImportOfTheSameName()
    {
        var first = await ImportAsync("clip.wav", [1]);
        var second = await ImportAsync("clip.wav", [2]);

        Assert.NotEqual(first, second);
        Assert.Equal<byte[]>([1], await File.ReadAllBytesAsync(first));
        Assert.Equal<byte[]>([2], await File.ReadAllBytesAsync(second));
    }

    [Fact]
    public async Task Delete_RemovesAFileTheLibraryOwns()
    {
        var path = await ImportAsync("gone.mp3", [1]);

        MediaLibrary.Delete(path);

        Assert.False(File.Exists(path));
    }

    /// <summary>
    /// The guard that matters. Boards built before media was imported still point at files in the
    /// user's own folders, and clearing such a pad must not reach out and delete the original.
    /// </summary>
    [Fact]
    public async Task Delete_LeavesFilesOutsideTheLibraryAlone()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"audiopad-outside-{Guid.NewGuid()}.mp3");
        await File.WriteAllBytesAsync(outside, [9]);
        _cleanup.Add(outside);

        MediaLibrary.Delete(outside);

        Assert.True(File.Exists(outside));
    }

    [Fact]
    public void IsManaged_IsFalseForNothingAndForOutsidePaths()
    {
        Assert.False(MediaLibrary.IsManaged(null));
        Assert.False(MediaLibrary.IsManaged("   "));
        Assert.False(MediaLibrary.IsManaged(Path.Combine(Path.GetTempPath(), "elsewhere.mp3")));
    }

    [Fact]
    public async Task IsManaged_IsTrueForAnImportedFile()
    {
        Assert.True(MediaLibrary.IsManaged(await ImportAsync("owned.mp3", [1])));
    }

    /// <summary>A name from an archive must not be able to write outside the media folder.</summary>
    [Fact]
    public async Task Import_CannotBeSteeredOutOfTheLibraryByTheSuppliedName()
    {
        Assert.True(MediaLibrary.IsManaged(await ImportAsync(Path.Combine("..", "..", "escaped.mp3"), [1])));
    }

    [Fact]
    public async Task Adopt_CopiesAFileFromOutsideIntoTheLibrary()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"audiopad-adopt-{Guid.NewGuid()}.mp3");
        await File.WriteAllBytesAsync(outside, [7, 7, 7]);
        _cleanup.Add(outside);

        var adopted = MediaLibrary.Adopt(outside, MediaLibrary.AudioFolder);
        _cleanup.Add(adopted!);

        Assert.True(MediaLibrary.IsManaged(adopted));
        Assert.Equal<byte[]>([7, 7, 7], await File.ReadAllBytesAsync(adopted!));

        // The original is copied, never moved — it isn't the app's file to take away.
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task Adopt_LeavesAFileAlreadyInTheLibraryWhereItIs()
    {
        var path = await ImportAsync("already-here.mp3", [1]);

        Assert.Equal(path, MediaLibrary.Adopt(path, MediaLibrary.AudioFolder));
    }

    /// <summary>A pad pointing at something that's since vanished keeps saying what it wanted.</summary>
    [Fact]
    public void Adopt_KeepsAMissingPathRatherThanDroppingIt()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"audiopad-missing-{Guid.NewGuid()}.mp3");

        Assert.Equal(missing, MediaLibrary.Adopt(missing, MediaLibrary.AudioFolder));
    }

    [Fact]
    public async Task AdoptAll_RewritesEveryExternalPathAndReportsTheChange()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"audiopad-setup-{Guid.NewGuid()}.mp3");
        await File.WriteAllBytesAsync(outside, [3]);
        _cleanup.Add(outside);

        var page = Models.Page.CreateDefault(rows: 1, columns: 1);
        page.Pads[0].AudioFilePath = outside;
        var setup = new Models.Setup { Pages = { page } };

        Assert.True(MediaLibrary.AdoptAll(setup));
        _cleanup.Add(page.Pads[0].AudioFilePath!);
        Assert.True(MediaLibrary.IsManaged(page.Pads[0].AudioFilePath));

        // The name it was known by is carried across, since the stored file no longer shows it.
        Assert.Equal(Path.GetFileName(outside), page.Pads[0].AudioFileName);

        // Nothing left outside, so a second pass has nothing to do.
        Assert.False(MediaLibrary.AdoptAll(setup));
    }

    private async Task<string> ImportAsync(string fileName, byte[] bytes)
    {
        await using var source = new MemoryStream(bytes);
        var path = await MediaLibrary.ImportAsync(source, fileName, MediaLibrary.AudioFolder);
        _cleanup.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _cleanup.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}
