namespace AudioPad.UI.ViewModels;

/// <summary>
/// The colours offered for pads. A fixed list rather than free text for the same reason the page's
/// header colour is one: a value that <c>Color.Parse</c> can't read would only fail later, when
/// the grid tries to draw itself.
/// </summary>
public static class PadColorPresets
{
    /// <summary>Darker shades, for pads at rest.</summary>
    public static readonly string[] Dark =
    [
        "#3A3A3A", "#4E4E4E", "#37474F", "#3E2723",
        "#1B2A38", "#22313F", "#2E3B2E", "#3A2E3F",
    ];

    /// <summary>Brighter shades, for pads that are playing or hovered.</summary>
    public static readonly string[] Bright =
    [
        "#FFC107", "#FFD866", "#FF7043", "#EF5350",
        "#AB47BC", "#5C6BC0", "#42A5F5", "#26A69A",
        "#66BB6A", "#FFA726", "#EC407A", "#8D6E63",
    ];

    /// <summary>Every preset, for pickers that shouldn't steer the choice either way.</summary>
    public static IEnumerable<string> All => Dark.Concat(Bright);

    /// <summary>
    /// Builds the entries for one picker, keeping a colour that's already saved selectable even if
    /// it isn't a preset — otherwise binding would find no match and silently wipe it on save.
    /// </summary>
    public static IReadOnlyList<PadColorChoice> BuildChoices(
        IEnumerable<string> presets,
        string? current,
        string? inheritLabel)
    {
        var choices = new List<PadColorChoice>();

        if (inheritLabel is not null)
        {
            choices.Add(new PadColorChoice(inheritLabel, null));
        }

        var values = presets.ToList();
        if (!string.IsNullOrWhiteSpace(current) && !values.Contains(current))
        {
            choices.Add(new PadColorChoice(current, current));
        }

        choices.AddRange(values.Select(value => new PadColorChoice(value, value)));
        return choices;
    }

    /// <summary>Finds the entry matching a saved value, falling back to the first (inherit) entry.</summary>
    public static PadColorChoice Select(IReadOnlyList<PadColorChoice> choices, string? value) =>
        choices.FirstOrDefault(choice => choice.Value == value) ?? choices[0];
}
