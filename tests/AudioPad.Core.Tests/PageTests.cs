using AudioPad.Core.Models;

namespace AudioPad.Core.Tests;

public class PageTests
{
    [Fact]
    public void CreateDefault_FillsOnePadPerCell()
    {
        var page = Page.CreateDefault(rows: 3, columns: 2);

        Assert.Equal(6, page.Pads.Count);
    }

    [Fact]
    public void FindPad_ReturnsPadAtThatPosition()
    {
        var page = Page.CreateDefault(rows: 2, columns: 2);

        var pad = page.FindPad(row: 1, column: 0);

        Assert.NotNull(pad);
        Assert.Equal(1, pad!.Row);
        Assert.Equal(0, pad.Column);
    }

    [Fact]
    public void FindPad_ReturnsNull_WhenNoPadAtPosition()
    {
        var page = new Page { Rows = 2, Columns = 2 };

        var pad = page.FindPad(row: 5, column: 5);

        Assert.Null(pad);
    }

    [Fact]
    public void Resize_Shrinking_DropsOutOfBoundsPads()
    {
        var page = Page.CreateDefault(rows: 3, columns: 3);

        page.Resize(rows: 2, columns: 2);

        Assert.Equal(4, page.Pads.Count);
        Assert.All(page.Pads, pad => Assert.True(pad.Row < 2 && pad.Column < 2));
    }

    [Fact]
    public void Resize_Growing_AddsBlankPads()
    {
        var page = Page.CreateDefault(rows: 2, columns: 2);

        page.Resize(rows: 3, columns: 3);

        Assert.Equal(9, page.Pads.Count);
    }

    [Fact]
    public void Resize_PreservesExistingPadData_WhenPositionStillInBounds()
    {
        var page = Page.CreateDefault(rows: 2, columns: 2);
        page.FindPad(0, 0)!.Label = "Applause";

        page.Resize(rows: 3, columns: 3);

        Assert.Equal("Applause", page.FindPad(0, 0)!.Label);
    }

    [Fact]
    public void Resize_DiscardsPadData_WhenPositionFallsOutOfBounds()
    {
        var page = Page.CreateDefault(rows: 2, columns: 2);
        page.FindPad(1, 1)!.Label = "Applause";

        page.Resize(rows: 1, columns: 1);

        Assert.Single(page.Pads);
        Assert.Null(page.FindPad(1, 1));
    }

    [Fact]
    public void Resize_LeavesPadsInRowMajorOrder()
    {
        // The UI lays pads out in collection order, so growing the grid must interleave the new
        // cells into position rather than appending them after the surviving ones.
        var page = Page.CreateDefault(rows: 2, columns: 2);

        page.Resize(rows: 3, columns: 3);

        var expected = new[] { (0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2), (2, 0), (2, 1), (2, 2) };
        Assert.Equal(expected, page.Pads.Select(pad => (pad.Row, pad.Column)));
    }

    [Fact]
    public void Resize_KeepsTheSamePadInstances_WhenPositionStillInBounds()
    {
        var page = Page.CreateDefault(rows: 2, columns: 2);
        var original = page.FindPad(1, 1)!;

        page.Resize(rows: 3, columns: 3);

        Assert.Same(original, page.FindPad(1, 1));
    }
}
