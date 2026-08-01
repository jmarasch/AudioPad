using AudioPad.Core.Models;

namespace AudioPad.Core.Tests;

public class PadPaletteResolverTests
{
    [Fact]
    public void Resolve_UsesBuiltInColorsWhenNothingIsSet()
    {
        var resolved = PadPaletteResolver.Resolve(new PadPalette(), new PadPalette());

        Assert.Equal(PadPaletteResolver.DefaultInactive, resolved.Inactive);
        Assert.Equal(PadPaletteResolver.DefaultActive, resolved.Active);
        Assert.Equal(PadPaletteResolver.DefaultInactiveHover, resolved.InactiveHover);
        Assert.Equal(PadPaletteResolver.DefaultActiveHover, resolved.ActiveHover);
    }

    [Fact]
    public void Resolve_PrefersThePagesColorsOverTheBuiltInOnes()
    {
        var page = new PadPalette { Inactive = "#111111", Active = "#222222" };

        var resolved = PadPaletteResolver.Resolve(new PadPalette(), page);

        Assert.Equal("#111111", resolved.Inactive);
        Assert.Equal("#222222", resolved.Active);
        Assert.Equal(PadPaletteResolver.DefaultActiveHover, resolved.ActiveHover);
    }

    [Fact]
    public void Resolve_PrefersThePadsOwnColorsOverThePages()
    {
        var pad = new PadPalette { Active = "#AAAAAA" };
        var page = new PadPalette { Inactive = "#111111", Active = "#222222" };

        var resolved = PadPaletteResolver.Resolve(pad, page);

        Assert.Equal("#AAAAAA", resolved.Active);
    }

    /// <summary>
    /// The point of resolving each colour separately: overriding one shade on a pad must not drag
    /// the other three off the page's defaults with it.
    /// </summary>
    [Fact]
    public void Resolve_OverridingOneColorLeavesTheOthersFollowingThePage()
    {
        var pad = new PadPalette { Active = "#AAAAAA" };
        var page = new PadPalette
        {
            Inactive = "#111111",
            Active = "#222222",
            InactiveHover = "#333333",
            ActiveHover = "#444444",
        };

        var resolved = PadPaletteResolver.Resolve(pad, page);

        Assert.Equal("#AAAAAA", resolved.Active);
        Assert.Equal("#111111", resolved.Inactive);
        Assert.Equal("#333333", resolved.InactiveHover);
        Assert.Equal("#444444", resolved.ActiveHover);
    }

    [Fact]
    public void Resolve_TreatsBlankAsUnset()
    {
        var pad = new PadPalette { Active = "   " };
        var page = new PadPalette { Active = "#222222" };

        Assert.Equal("#222222", PadPaletteResolver.Resolve(pad, page).Active);
    }

    [Fact]
    public void Resolve_HandlesAPageAndPadThatHaveNoPalettesAtAll()
    {
        var resolved = PadPaletteResolver.Resolve(null, null);

        Assert.Equal(PadPaletteResolver.DefaultInactive, resolved.Inactive);
        Assert.Equal(PadPaletteResolver.DefaultActive, resolved.Active);
    }
}
