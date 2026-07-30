using AudioPad.Core.Models;
using AudioPad.Core.Persistence;

namespace AudioPad.Core.Tests;

public class ProfileRepositoryTests : IDisposable
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), $"audiopad-test-{Guid.NewGuid()}.json");
    private readonly ProfileRepository _repository = new();

    [Fact]
    public void SaveThenLoad_RoundTripsProfileContents()
    {
        var original = GridProfile.CreateDefault(rows: 2, columns: 3);
        original.Pads[0].Label = "Applause";
        original.Pads[0].Mode = PlaybackMode.Loop;
        original.Pads[0].Volume = 0.5f;

        _repository.SaveProfile(_tempPath, original);
        var loaded = _repository.LoadProfile(_tempPath);

        Assert.Equal(original.Rows, loaded.Rows);
        Assert.Equal(original.Columns, loaded.Columns);
        Assert.Equal(original.Pads.Count, loaded.Pads.Count);

        var loadedPad = loaded.FindPad(row: 0, column: 0);
        Assert.NotNull(loadedPad);
        Assert.Equal("Applause", loadedPad!.Label);
        Assert.Equal(PlaybackMode.Loop, loadedPad.Mode);
        Assert.Equal(0.5f, loadedPad.Volume);
    }

    [Fact]
    public void LoadProfile_ReturnsDefault_WhenFileDoesNotExist()
    {
        var loaded = _repository.LoadProfile(_tempPath);

        Assert.Equal(4, loaded.Rows);
        Assert.Equal(4, loaded.Columns);
        Assert.Equal(16, loaded.Pads.Count);
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }
    }
}
