namespace AudioPad.Core.Models;

/// <summary>One page of the setup: a titled, themed grid of pads, shown as one slide of the carousel.</summary>
public sealed class Page
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; set; } = "Page 1";

    /// <summary>Hex color (e.g. "#FF7043") used to tint this page's header/background in the UI.</summary>
    public string ThemeColor { get; set; } = "#FF7043";

    public int Rows { get; set; } = 4;

    public int Columns { get; set; } = 4;

    public List<PadConfig> Pads { get; set; } = new();

    /// <summary>Builds a default page with one blank, unconfigured pad per grid cell.</summary>
    public static Page CreateDefault(string title = "Page 1", int rows = 4, int columns = 4)
    {
        var page = new Page { Title = title, Rows = rows, Columns = columns };

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                page.Pads.Add(new PadConfig { Row = row, Column = column });
            }
        }

        return page;
    }

    /// <summary>Finds the configured pad at a grid position, or null if none exists there yet.</summary>
    public PadConfig? FindPad(int row, int column) =>
        Pads.FirstOrDefault(pad => pad.Row == row && pad.Column == column);

    /// <summary>
    /// Changes the grid size: adds blank pads for newly in-bounds cells, drops pads that fall
    /// outside the new bounds, and leaves everything else untouched. Rebuilds the list in
    /// row-major order rather than appending, because the UI lays pads out in collection order —
    /// appending new cells at the end would scatter them across the wrong grid positions.
    /// </summary>
    public void Resize(int rows, int columns)
    {
        var resized = new List<PadConfig>(rows * columns);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                resized.Add(FindPad(row, column) ?? new PadConfig { Row = row, Column = column });
            }
        }

        Pads = resized;
        Rows = rows;
        Columns = columns;
    }
}
