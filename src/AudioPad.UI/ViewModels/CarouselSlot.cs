namespace AudioPad.UI.ViewModels;

/// <summary>
/// One slide of the page carousel.
///
/// The endless-carousel illusion needs the last page to appear again before the first and the
/// first again after the last. Listing the same <see cref="PageViewModel"/> at those extra indices
/// looked harmless, but an ItemsControl identifies its realized containers *by item* — with one
/// object appearing three times that mapping is ambiguous, and every reorder degraded it further
/// until pages rendered on top of each other. Restarting the app cleared it only because the
/// containers were built afresh. Wrapping each slot in its own instance keeps every item distinct,
/// so container reuse stays correct no matter how often the pages are rearranged.
/// </summary>
public sealed class CarouselSlot(PageViewModel page)
{
    public PageViewModel Page { get; } = page;
}
