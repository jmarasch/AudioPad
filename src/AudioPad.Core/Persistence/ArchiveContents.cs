using AudioPad.Core.Models;

namespace AudioPad.Core.Persistence;

/// <summary>
/// What an AudioPad archive turned out to hold — exactly one of the two is set.
///
/// Both kinds of archive have the same file extension, because asking someone to remember whether
/// a file is "a page" or "a whole setup" before opening it is a question the file can answer
/// itself. Importing reads which one it is and the caller decides what that means: a setup is the
/// board in its entirety, a page is one board to add to it.
/// </summary>
/// <param name="Page">The single page the archive held, or null if it held a whole setup.</param>
/// <param name="Setup">The whole setup the archive held, or null if it held a single page.</param>
public sealed record ArchiveContents(Page? Page, Setup? Setup);
