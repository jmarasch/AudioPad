namespace AudioPad.Core.Models;

/// <summary>A saved layout: grid dimensions plus the configuration for every pad in it.</summary>
public sealed class GridProfile
{
    public int Rows { get; set; } = 4;

    public int Columns { get; set; } = 4;

    public List<PadConfig> Pads { get; set; } = new();

    /// <summary>Builds a default profile with one blank, unconfigured pad per grid cell.</summary>
    public static GridProfile CreateDefault(int rows = 4, int columns = 4)
    {
        var profile = new GridProfile { Rows = rows, Columns = columns };

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                profile.Pads.Add(new PadConfig { Row = row, Column = column });
            }
        }

        return profile;
    }

    /// <summary>Finds the configured pad at a grid position, or null if none exists there yet.</summary>
    public PadConfig? FindPad(int row, int column) =>
        Pads.FirstOrDefault(pad => pad.Row == row && pad.Column == column);
}
