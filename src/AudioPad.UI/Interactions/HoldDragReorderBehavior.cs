using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AudioPad.UI.Interactions;

/// <summary>
/// Attaches "hold, then drag to a new spot" reordering to one item in a same-type collection (pads
/// in a page's grid, page tiles in the overview). Built on Avalonia's native long-press gesture
/// (<see cref="InputElement.HoldingEvent"/>, with mouse-holding enabled too, so it's testable on
/// Desktop) rather than <c>Avalonia.Input.DragDrop</c>, since it needs full control over the drag
/// visual instead of an OS-level drag cursor, and has to work identically for a fixed grid of pads
/// and a reflowing wrap of page tiles.
///
/// Reordering happens live: crossing onto another item applies the move immediately, so the rest
/// of the collection shifts and the layout always previews what releasing will leave behind.
///
/// Once the hold is confirmed the drag is tracked from the <see cref="TopLevel"/> rather than from
/// this item. Each reorder makes the items control recycle containers, which destroys the pointer
/// capture held by the dragged item — tracking from the item therefore ended the drag after a
/// single step, letting an item move only to its immediate neighbour. The TopLevel outlives every
/// container, so the gesture survives any number of reorders.
/// </summary>
/// <typeparam name="TItem">The DataContext type of one reorderable item (e.g. PadViewModel).</typeparam>
public sealed class HoldDragReorderBehavior<TItem>
    where TItem : class
{
    private const double DraggedScale = 0.88;
    private const double DimmedOpacity = 0.45;

    private readonly InputElement _item;
    private readonly Action<object?, TItem, TItem> _onMove;
    private readonly List<Control> _dimmed = [];

    private TopLevel? _topLevel;
    private ItemsControl? _owner;
    private TItem? _dragged;
    private TItem? _lastTarget;

    /// <summary>
    /// The collection owner (the items control's DataContext), captured when the drag starts. Each
    /// reorder recycles containers and detaches this item from the visual tree, so resolving the
    /// owner by walking up from the item works only for the first move and silently returns null
    /// after that — which stopped every drag dead one slot from where it began.
    /// </summary>
    private object? _ownerContext;
    private Control? _draggedVisual;
    private Carousel? _suppressedCarousel;
    private ScrollViewer? _suppressedScroll;
    private ScrollBarVisibility _scrollHorizontalWas;
    private ScrollBarVisibility _scrollVerticalWas;

    public HoldDragReorderBehavior(InputElement item, Action<object?, TItem, TItem> onMove)
    {
        _item = item;
        _onMove = onMove;

        InputElement.SetIsHoldingEnabled(item, true);
        InputElement.SetIsHoldWithMouseEnabled(item, true);

        // Registered with handledEventsToo: true because Button marks its own PointerPressed
        // handling as Handled (for its Click/pressed-state bookkeeping), which would otherwise
        // stop a plain `+=` subscription from ever firing.
        item.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, handledEventsToo: true);
        item.Holding += OnHolding;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _topLevel = TopLevel.GetTopLevel(_item);
        _owner = _item.FindAncestorOfType<ItemsControl>();
    }

    private void OnHolding(object? sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState != HoldingState.Started || _topLevel is null || _item.DataContext is not TItem item)
        {
            return;
        }

        _dragged = item;
        _owner ??= _item.FindAncestorOfType<ItemsControl>();
        _ownerContext = _owner?.DataContext;

        // Deliberately not listening for PointerCaptureLost: every reorder recycles containers and
        // drops their capture, which is routine here — reacting to it is what limited a drag to a
        // single neighbouring slot. Release is the only thing that ends the drag.
        _topLevel.AddHandler(InputElement.PointerMovedEvent, OnTopLevelPointerMoved, handledEventsToo: true);
        _topLevel.AddHandler(InputElement.PointerReleasedEvent, OnTopLevelPointerReleased, handledEventsToo: true);

        SuppressCarouselSwipe();
        SuppressAncestorScrolling();
        RefreshDragVisuals();
        e.Handled = true;
    }

    private void OnTopLevelPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragged is not { } source || _topLevel is null)
        {
            return;
        }

        var hit = HitTestItem(e.GetPosition(_topLevel), source);

        // Pointer moves arrive ~60x a second, and after a reorder the item just displaced is
        // usually still under the finger — so reordering on every move made the pair trade places
        // repeatedly and the drag could never advance past one slot. Only a *change* of target is
        // a new intent to reorder. Landing back over the dragged item clears this, so returning to
        // a previous neighbour later still works.
        if (hit is null || ReferenceEquals(hit, _lastTarget))
        {
            if (hit is null)
            {
                _lastTarget = null;
            }

            return;
        }


        _lastTarget = hit;
        _onMove(_ownerContext, source, hit);

        // The reorder recycles containers, so the shrink and the dimming have to be re-resolved
        // against the new arrangement or they end up applied to the wrong items.
        RefreshDragVisuals();
    }

    private void OnTopLevelPointerReleased(object? sender, PointerReleasedEventArgs e) => EndDrag();

    private void EndDrag()
    {
        if (_topLevel is not null)
        {
            _topLevel.RemoveHandler(InputElement.PointerMovedEvent, OnTopLevelPointerMoved);
            _topLevel.RemoveHandler(InputElement.PointerReleasedEvent, OnTopLevelPointerReleased);
        }

        _dragged = null;
        _lastTarget = null;
        _ownerContext = null;
        ClearDraggedVisual();
        RestoreDimmedItems();
        RestoreCarouselSwipe();
        RestoreAncestorScrolling();
    }

    /// <summary>
    /// Stops the surrounding ScrollViewer from panning mid-drag. Page tiles live in a ListBox, so
    /// without this the scroll gesture keeps stealing the pointer the same way the Carousel's
    /// swipe did — which is why dragging tiles felt worse than dragging pads.
    /// </summary>
    private void SuppressAncestorScrolling()
    {
        if (_item.FindAncestorOfType<ScrollViewer>() is not { } scroll)
        {
            return;
        }

        _suppressedScroll = scroll;
        _scrollHorizontalWas = scroll.HorizontalScrollBarVisibility;
        _scrollVerticalWas = scroll.VerticalScrollBarVisibility;
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    private void RestoreAncestorScrolling()
    {
        if (_suppressedScroll is { } scroll)
        {
            scroll.HorizontalScrollBarVisibility = _scrollHorizontalWas;
            scroll.VerticalScrollBarVisibility = _scrollVerticalWas;
            _suppressedScroll = null;
        }
    }

    /// <summary>
    /// Finds the topmost reorderable item under the pointer other than the one being dragged, so
    /// the dragged item (which sits under the pointer by definition) can't match itself.
    /// </summary>
    private TItem? HitTestItem(Point point, TItem source)
    {
        if (_topLevel is null)
        {
            return null;
        }

        foreach (var visual in _topLevel.GetVisualsAt(point))
        {
            if (VisualTreeHelpers.FindAncestorDataContext<TItem>(visual) is { } item
                && !ReferenceEquals(item, source))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Shrinks whichever container currently shows the dragged item and fades the rest.</summary>
    private void RefreshDragVisuals()
    {
        ClearDraggedVisual();
        RestoreDimmedItems();

        if (_owner is null || _dragged is null)
        {
            return;
        }

        foreach (var container in _owner.GetRealizedContainers())
        {
            if (ReferenceEquals(container.DataContext, _dragged))
            {
                container.RenderTransformOrigin = RelativePoint.Center;
                container.RenderTransform = new ScaleTransform(DraggedScale, DraggedScale);
                container.ZIndex = 100;
                _draggedVisual = container;
            }
            else
            {
                container.Opacity = DimmedOpacity;
                _dimmed.Add(container);
            }
        }
    }

    private void ClearDraggedVisual()
    {
        if (_draggedVisual is { } visual)
        {
            visual.RenderTransform = null;
            visual.ZIndex = 0;
            _draggedVisual = null;
        }
    }

    private void RestoreDimmedItems()
    {
        foreach (var container in _dimmed)
        {
            container.Opacity = 1;
        }

        _dimmed.Clear();
    }

    /// <summary>
    /// Turns off the ancestor Carousel's swipe for the duration of a confirmed hold. Without this
    /// the first sideways movement of a drag is claimed by the Carousel's swipe recognizer and the
    /// page changes instead of the item moving. The hold is the user committing to a drag, so it
    /// wins over the swipe until the pointer is released.
    /// </summary>
    private void SuppressCarouselSwipe()
    {
        if (_item.FindAncestorOfType<Carousel>() is { IsSwipeEnabled: true } carousel)
        {
            carousel.IsSwipeEnabled = false;
            _suppressedCarousel = carousel;
        }
    }

    private void RestoreCarouselSwipe()
    {
        if (_suppressedCarousel is { } carousel)
        {
            carousel.IsSwipeEnabled = true;
            _suppressedCarousel = null;
        }
    }
}
