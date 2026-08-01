namespace AudioPad.Core.Models;

/// <summary>
/// The four colours a pad can be drawn in — idle and playing, each with its own hover shade.
///
/// Hover needs to be stated separately for each state rather than derived: a single hover colour
/// is the reason hovering a *playing* pad turned it dark, throwing away the one piece of feedback
/// that matters most while a clip is running.
///
/// Every entry is nullable and means "not specified here". The same type carries a page's defaults
/// and a pad's overrides, so resolution is one rule at every level — see
/// <see cref="PadPaletteResolver"/>.
/// </summary>
public sealed class PadPalette
{
    /// <summary>Colour when the pad is idle.</summary>
    public string? Inactive { get; set; }

    /// <summary>Colour when the pad is playing.</summary>
    public string? Active { get; set; }

    /// <summary>Colour when the pointer is over an idle pad.</summary>
    public string? InactiveHover { get; set; }

    /// <summary>Colour when the pointer is over a playing pad.</summary>
    public string? ActiveHover { get; set; }

    /// <summary>True when nothing is set, i.e. this level contributes nothing to resolution.</summary>
    public bool IsEmpty => Inactive is null && Active is null && InactiveHover is null && ActiveHover is null;

    /// <summary>Copies the four values, so an editable working copy can't mutate the saved one.</summary>
    public PadPalette Clone() => new()
    {
        Inactive = Inactive,
        Active = Active,
        InactiveHover = InactiveHover,
        ActiveHover = ActiveHover,
    };
}
