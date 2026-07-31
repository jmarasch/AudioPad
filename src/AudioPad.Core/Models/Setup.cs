namespace AudioPad.Core.Models;

/// <summary>The full saved state: an ordered collection of pages, shown as an endless carousel.</summary>
public sealed class Setup
{
    public List<Page> Pages { get; set; } = new();

    /// <summary>Builds a default setup containing a single default page.</summary>
    public static Setup CreateDefault() => new() { Pages = { Page.CreateDefault() } };
}
