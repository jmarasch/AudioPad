using AudioPad.Core.Models;
using AudioPad.Core.Persistence;

namespace AudioPad.Core.Tests;

public class SetupArchiveTests : IDisposable
{
    private readonly string _sourceAudioPath = Path.Combine(Path.GetTempPath(), $"audiopad-test-audio-{Guid.NewGuid()}.wav");
    private readonly byte[] _audioBytes = [1, 2, 3, 4, 5];

    public SetupArchiveTests()
    {
        File.WriteAllBytes(_sourceAudioPath, _audioBytes);
    }

    [Fact]
    public async Task ExportPage_ThenImportPage_RoundTripsStructureAndMediaBytes()
    {
        var page = Page.CreateDefault(title: "Effects", rows: 1, columns: 1);
        page.Pads[0].Label = "Applause";
        page.Pads[0].AudioFilePath = _sourceAudioPath;

        await using var archive = new MemoryStream();
        await SetupArchive.ExportPageAsync(archive, page);
        archive.Position = 0;

        var imported = await SetupArchive.ImportPageAsync(archive);

        Assert.Equal("Effects", imported.Title);
        Assert.Equal("Applause", imported.Pads[0].Label);
        Assert.NotNull(imported.Pads[0].AudioFilePath);
        Assert.NotEqual(_sourceAudioPath, imported.Pads[0].AudioFilePath);
        Assert.True(File.Exists(imported.Pads[0].AudioFilePath));
        Assert.Equal(_audioBytes, await File.ReadAllBytesAsync(imported.Pads[0].AudioFilePath!));
    }

    [Fact]
    public async Task ExportSetup_ThenImportSetup_RoundTripsMultiplePagesAndDedupesSharedMedia()
    {
        var pageA = Page.CreateDefault(title: "A", rows: 1, columns: 2);
        pageA.Pads[0].AudioFilePath = _sourceAudioPath;
        pageA.Pads[1].AudioFilePath = _sourceAudioPath; // same source file, referenced twice
        var pageB = Page.CreateDefault(title: "B", rows: 1, columns: 1);
        var setup = new Setup { Pages = { pageA, pageB } };

        await using var archive = new MemoryStream();
        await SetupArchive.ExportSetupAsync(archive, setup);
        archive.Position = 0;

        var imported = await SetupArchive.ImportSetupAsync(archive);

        Assert.Equal(2, imported.Pages.Count);
        Assert.Equal("A", imported.Pages[0].Title);
        Assert.Equal("B", imported.Pages[1].Title);
        Assert.Equal(
            imported.Pages[0].Pads[0].AudioFilePath,
            imported.Pages[0].Pads[1].AudioFilePath);
    }

    public void Dispose()
    {
        if (File.Exists(_sourceAudioPath))
        {
            File.Delete(_sourceAudioPath);
        }
    }
}
