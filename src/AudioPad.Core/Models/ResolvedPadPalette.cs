namespace AudioPad.Core.Models;

/// <summary>A pad's four colours once every fallback has been applied, so none of them is null.</summary>
/// <param name="Inactive">Colour when the pad is idle.</param>
/// <param name="Active">Colour when the pad is playing.</param>
/// <param name="InactiveHover">Colour when the pointer is over an idle pad.</param>
/// <param name="ActiveHover">Colour when the pointer is over a playing pad.</param>
public readonly record struct ResolvedPadPalette(
    string Inactive,
    string Active,
    string InactiveHover,
    string ActiveHover);
