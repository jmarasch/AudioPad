namespace AudioPad.Core.Models;

/// <summary>
/// Works out what colour a pad actually is, given that a colour may be stated on the pad, on its
/// page, or nowhere at all.
///
/// Each of the four colours falls back independently, so a pad can override just its playing
/// colour and still follow the page for the rest — overriding one shade shouldn't drag the other
/// three along with it.
/// </summary>
public static class PadPaletteResolver
{
    /// <summary>The look a pad has when neither it nor its page says otherwise.</summary>
    public const string DefaultInactive = "#3A3A3A";
    public const string DefaultActive = "#FFC107";
    public const string DefaultInactiveHover = "#4E4E4E";
    public const string DefaultActiveHover = "#FFD866";

    /// <summary>The built-in colours, as a palette — what a page starts out with.</summary>
    public static PadPalette CreateDefaults() => new()
    {
        Inactive = DefaultInactive,
        Active = DefaultActive,
        InactiveHover = DefaultInactiveHover,
        ActiveHover = DefaultActiveHover,
    };

    /// <summary>Resolves a pad's colours: its own where set, otherwise its page's, otherwise built-in.</summary>
    public static ResolvedPadPalette Resolve(PadPalette? pad, PadPalette? page) => new(
        Pick(pad?.Inactive, page?.Inactive, DefaultInactive),
        Pick(pad?.Active, page?.Active, DefaultActive),
        Pick(pad?.InactiveHover, page?.InactiveHover, DefaultInactiveHover),
        Pick(pad?.ActiveHover, page?.ActiveHover, DefaultActiveHover));

    private static string Pick(string? padValue, string? pageValue, string fallback) =>
        Blank(padValue) ? (Blank(pageValue) ? fallback : pageValue!) : padValue!;

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
}
