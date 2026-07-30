using AudioPad.Core.Models;

namespace AudioPad.Core.Tests;

public class GridProfileTests
{
    [Fact]
    public void CreateDefault_FillsOnePadPerCell()
    {
        var profile = GridProfile.CreateDefault(rows: 3, columns: 2);

        Assert.Equal(6, profile.Pads.Count);
    }

    [Fact]
    public void FindPad_ReturnsPadAtThatPosition()
    {
        var profile = GridProfile.CreateDefault(rows: 2, columns: 2);

        var pad = profile.FindPad(row: 1, column: 0);

        Assert.NotNull(pad);
        Assert.Equal(1, pad!.Row);
        Assert.Equal(0, pad.Column);
    }

    [Fact]
    public void FindPad_ReturnsNull_WhenNoPadAtPosition()
    {
        var profile = new GridProfile { Rows = 2, Columns = 2 };

        var pad = profile.FindPad(row: 5, column: 5);

        Assert.Null(pad);
    }
}
