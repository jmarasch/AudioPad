using AudioPad.Core.Models;
using AudioPad.Core.Persistence;

namespace AudioPad.Core.Tests;

public class SetupRepositoryTests : IDisposable
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), $"audiopad-test-{Guid.NewGuid()}.json");
    private readonly SetupRepository _repository = new();

    [Fact]
    public void SaveThenLoad_RoundTripsMultiplePages()
    {
        var original = new Setup
        {
            Pages =
            {
                Page.CreateDefault(title: "Sound Effects", rows: 2, columns: 3),
                Page.CreateDefault(title: "Music", rows: 4, columns: 4),
            },
        };
        original.Pages[0].ThemeColor = "#00FF00";
        original.Pages[0].Pads[0].Label = "Applause";
        original.Pages[0].Pads[0].Mode = PlaybackMode.Loop;
        original.Pages[0].Pads[0].Volume = 0.5f;

        _repository.SaveSetup(_tempPath, original);
        var loaded = _repository.LoadSetup(_tempPath);

        Assert.Equal(2, loaded.Pages.Count);
        Assert.Equal("Sound Effects", loaded.Pages[0].Title);
        Assert.Equal("#00FF00", loaded.Pages[0].ThemeColor);
        Assert.Equal(original.Pages[0].Rows, loaded.Pages[0].Rows);
        Assert.Equal(original.Pages[0].Columns, loaded.Pages[0].Columns);

        var loadedPad = loaded.Pages[0].FindPad(row: 0, column: 0);
        Assert.NotNull(loadedPad);
        Assert.Equal("Applause", loadedPad!.Label);
        Assert.Equal(PlaybackMode.Loop, loadedPad.Mode);
        Assert.Equal(0.5f, loadedPad.Volume);

        Assert.Equal("Music", loaded.Pages[1].Title);
    }

    [Fact]
    public void LoadSetup_ReturnsDefaultSinglePage_WhenFileDoesNotExist()
    {
        var loaded = _repository.LoadSetup(_tempPath);

        Assert.Single(loaded.Pages);
        Assert.Equal(4, loaded.Pages[0].Rows);
        Assert.Equal(4, loaded.Pages[0].Columns);
        Assert.Equal(16, loaded.Pages[0].Pads.Count);
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }
    }
}
