namespace AudioPad.UI.ViewModels;

/// <summary>
/// One entry in a colour picker. <see cref="Value"/> is null for the "follow the page" entry, so
/// choosing it clears the override rather than storing a colour that merely happens to match the
/// page today — later changes to the page then still carry through.
/// </summary>
/// <param name="Name">What the entry is called in the list.</param>
/// <param name="Value">The colour, or null to inherit.</param>
public sealed record PadColorChoice(string Name, string? Value)
{
    /// <summary>Swatch colour for the entry, using a neutral placeholder for "inherit".</summary>
    public string Swatch => Value ?? "#00000000";
}
