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
/// and a vertical list of page tiles.
///
/// Nothing is reordered until the finger lifts. During the drag the dragged item simply follows the
/// pointer and a line shows the gap it would drop into; the collection itself is untouched, and one
/// move — and one save — happens on release.
///
/// That is a deliberate reversal of how this worked before, where each crossed item was reordered
/// live. Reordering mid-drag slides the dragged item *to* where the pointer already is, so the
/// pointer ends up over the item it just moved; the hit test then finds nothing, and a pixel of
/// jitter back across the boundary reads as a fresh target. The result was the same pair trading
/// places over and over — a reorder on roughly two of every three pointer moves, each one also
/// recycling the containers being hit-tested and (until it was moved to drag-end) writing the whole
/// setup to disk. Deciding once, at the end, removes the feedback loop rather than damping it.
/// </summary>
/// <typeparam name="TItem">The DataContext type of one reorderable item (e.g. PadViewModel).</typeparam>
public sealed class HoldDragReorderBehavior<TItem>
    where TItem : class
{
    private const double DraggedScale = 0.92;
    private const double DraggedOpacity = 0.75;
    private const double IndicatorThickness = 4;

    /// <summary>Marks drop lines in the shared overlay layer so leftovers can be identified.</summary>
    private const string IndicatorTag = "AudioPadDropIndicator";

    private readonly InputElement _item;
    private readonly Action<object?, TItem, int> _onDropped;

    private TopLevel? _topLevel;
    private ItemsControl? _owner;

    /// <summary>
    /// The collection owner (the items control's DataContext), captured when the drag starts rather
    /// than resolved on use: the item can be detached from the visual tree by the time the drop is
    /// applied, and walking up from a detached control silently returns null.
    /// </summary>
    private object? _ownerContext;

    private TItem? _dragged;

    /// <summary>
    /// The pointer that started the drag. Moves are filtered to it because the TopLevel handlers
    /// see every pointer: a second finger — landing to pinch, or just resting on the screen —
    /// otherwise dragged the item from its own position, somewhere else entirely on the page.
    /// </summary>
    private int? _dragPointerId;

    private IPointer? _pressedPointer;
    private int _pressedPointerId;
    private Point _pressedPoint;

    /// <summary>Pointer moves handled since the hold was confirmed. Guards the capture-lost exit.</summary>
    private int _movesSeen;

    private int _sourceIndex = -1;

    /// <summary>Where the item would be inserted right now, as a gap index in 0..ItemCount.</summary>
    private int _insertSlot = -1;

    private Control? _draggedVisual;
    private TranslateTransform? _follow;
    private OverlayLayer? _indicatorLayer;
    private Border? _indicator;

    private Carousel? _suppressedCarousel;
    private ScrollViewer? _pinnedScroll;
    private Vector _pinnedOffset;

    /// <param name="item">The control for one reorderable item.</param>
    /// <param name="onDropped">
    /// Applies the whole reorder, once, when the finger lifts: the item and the index it should end
    /// up at. Saving belongs here too — there is exactly one of these per drag.
    /// </param>
    public HoldDragReorderBehavior(InputElement item, Action<object?, TItem, int> onDropped)
    {
        _item = item;
        _onDropped = onDropped;

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
        // Safety net: if a previous drag on this item never received its release — some other
        // gesture took the pointer and swallowed it — pressing again clears it rather than leaving
        // the item stuck lifted with the list pinned.
        if (_dragged is not null)
        {
            GestureLog.Write("stale drag cleared on new press");
            EndDrag(applyDrop: false);
        }

        _topLevel = TopLevel.GetTopLevel(_item);
        _owner = _item.FindAncestorOfType<ItemsControl>();

        // HoldingRoutedEventArgs carries no pointer, so the one that could become a drag has to be
        // remembered from the press that started the hold.
        _pressedPointer = e.Pointer;
        _pressedPointerId = e.Pointer.Id;
        _pressedPoint = _topLevel is null ? default : e.GetPosition(_topLevel);
    }

    private void OnHolding(object? sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState != HoldingState.Started || _topLevel is null || _item.DataContext is not TItem item)
        {
            return;
        }

        _owner ??= _item.FindAncestorOfType<ItemsControl>();
        if (_owner?.ContainerFromItem(item) is not { } container)
        {
            return;
        }

        // Ends any drag that never got its release, before this one adds a second set of handlers.
        ActiveDrag.Begin(this, () => EndDrag(applyDrop: false));

        _dragged = item;
        _dragPointerId = _pressedPointerId;
        _ownerContext = _owner.DataContext;
        _draggedVisual = container;
        _sourceIndex = _owner.IndexFromContainer(container);
        _insertSlot = _sourceIndex;

        GestureLog.Write($"drag start item={typeof(TItem).Name} index={_sourceIndex} pointer={_dragPointerId}");

        _topLevel.AddHandler(InputElement.PointerMovedEvent, OnTopLevelPointerMoved, handledEventsToo: true);
        _topLevel.AddHandler(InputElement.PointerReleasedEvent, OnTopLevelPointerReleased, handledEventsToo: true);
        _topLevel.AddHandler(InputElement.PointerCaptureLostEvent, OnTopLevelPointerCaptureLost, handledEventsToo: true);

        // Take the pointer for the drag. Without this the release was being delivered somewhere
        // else entirely and never reached the handler above, so the drag never ended: the item
        // stayed shrunk and its drop line stayed on screen, one set left behind per attempt.
        // Capturing on the item is safe again now that nothing is reordered mid-drag — it was the
        // container recycling of live reordering that used to tear this capture down.
        _pressedPointer?.Capture(_item);

        SuppressCarouselSwipe();
        SuppressAncestorScrolling();
        LiftDraggedItem();
        e.Handled = true;
    }

    /// <summary>Marks the container as picked up: shrunk, faded, above its neighbours, and free to
    /// follow the pointer.</summary>
    private void LiftDraggedItem()
    {
        if (_draggedVisual is not { } visual)
        {
            return;
        }

        _follow = new TranslateTransform();
        visual.RenderTransformOrigin = RelativePoint.Center;
        visual.RenderTransform = new TransformGroup
        {
            Children = { new ScaleTransform(DraggedScale, DraggedScale), _follow },
        };
        visual.Opacity = DraggedOpacity;
        visual.ZIndex = 100;
    }

    private void OnTopLevelPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragged is null || _topLevel is null || e.Pointer.Id != _dragPointerId)
        {
            return;
        }

        _movesSeen++;
        var point = e.GetPosition(_topLevel);

        if (_follow is { } follow)
        {
            follow.X = point.X - _pressedPoint.X;
            follow.Y = point.Y - _pressedPoint.Y;
        }

        UpdateInsertSlot(point);
    }

    private void OnTopLevelPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Id == _dragPointerId)
        {
            EndDrag(applyDrop: true);
            return;
        }

        // A release from a pointer that isn't ours means our own release was swallowed and the
        // finger is long gone — the drag is over whether or not we were told. It is torn down
        // without applying anything, since by now the drop line reflects an interaction the user
        // has already moved on from.
        GestureLog.Write($"ending drag on foreign release={e.Pointer.Id}, ours was {_dragPointerId}");
        EndDrag(applyDrop: false);
    }

    /// <summary>
    /// Second way out of a drag, for when the pointer is taken by something else — a pan or pinch
    /// recogniser winning it — and its release is therefore delivered to that instead of here.
    /// Whatever the drag had settled on is still applied, since losing the pointer is not the user
    /// changing their mind.
    ///
    /// It only counts once the drag has actually moved: capture changes hands around the moment a
    /// hold is recognised, and reacting to that would end every drag the instant it began.
    /// </summary>
    private void OnTopLevelPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dragged is null || e.Pointer.Id != _dragPointerId)
        {
            return;
        }

        GestureLog.Write($"capture lost pointer={e.Pointer.Id} moves={_movesSeen}");

        if (_movesSeen > 0)
        {
            EndDrag(applyDrop: true);
        }
    }

    /// <summary>
    /// Puts everything back the way it was and, for a normal release, applies the single reorder the
    /// drag was describing. <paramref name="applyDrop"/> is false when the drag was interrupted
    /// rather than finished, so an abandoned gesture leaves the order untouched.
    /// </summary>
    private void EndDrag(bool applyDrop)
    {
        if (_dragged is null)
        {
            return;
        }

        ActiveDrag.Finish(this);

        if (_topLevel is not null)
        {
            _topLevel.RemoveHandler(InputElement.PointerMovedEvent, OnTopLevelPointerMoved);
            _topLevel.RemoveHandler(InputElement.PointerReleasedEvent, OnTopLevelPointerReleased);
            _topLevel.RemoveHandler(InputElement.PointerCaptureLostEvent, OnTopLevelPointerCaptureLost);
        }

        _movesSeen = 0;

        var dragged = _dragged;
        var destination = DestinationIndex();

        DropDraggedItem();
        HideIndicator();
        RestoreCarouselSwipe();
        RestoreAncestorScrolling();

        _dragged = null;
        _dragPointerId = null;

        GestureLog.Write($"drag end apply={applyDrop} from={_sourceIndex} slot={_insertSlot} to={destination}");

        if (applyDrop && dragged is not null && destination >= 0 && destination != _sourceIndex)
        {
            _onDropped(_ownerContext, dragged, destination);
        }

        _ownerContext = null;
        _sourceIndex = -1;
        _insertSlot = -1;
    }

    /// <summary>
    /// Converts the insertion gap into the index the item ends up at. Removing the item first
    /// shifts every later gap down by one, so a gap beyond the item's own position is one higher
    /// than the index it lands on.
    /// </summary>
    private int DestinationIndex()
    {
        if (_insertSlot < 0 || _sourceIndex < 0)
        {
            return -1;
        }

        return _insertSlot > _sourceIndex ? _insertSlot - 1 : _insertSlot;
    }

    private void DropDraggedItem()
    {
        if (_draggedVisual is { } visual)
        {
            visual.RenderTransform = null;
            visual.Opacity = 1;
            visual.ZIndex = 0;
            _draggedVisual = null;
        }

        _follow = null;
    }

    /// <summary>
    /// Works out which gap between items the pointer is currently nearest, and moves the indicator
    /// line there.
    /// </summary>
    private void UpdateInsertSlot(Point point)
    {
        var containers = SettledContainerBounds();
        if (containers.Count == 0)
        {
            return;
        }

        var stacked = IsVerticallyStacked(containers);
        var nearest = NearestContainer(containers, point);
        var centre = nearest.Bounds.Center;
        var isAfter = stacked ? point.Y > centre.Y : point.X > centre.X;
        var slot = nearest.Index + (isAfter ? 1 : 0);

        if (slot != _insertSlot)
        {
            _insertSlot = slot;
            GestureLog.Write($"slot={slot} near={nearest.Index} after={isAfter}");
        }

        ShowIndicator(containers, stacked);
    }

    /// <summary>
    /// The containers that are still sitting in their normal places, with bounds in TopLevel
    /// coordinates, ordered by item index.
    ///
    /// The dragged item is deliberately left out. Its container is translated to follow the
    /// pointer, and <see cref="Visual.TranslatePoint"/> reports post-transform bounds — so its
    /// rectangle travels with the finger and contains the pointer at every moment of the drag.
    /// Included, it won its own hit test every time and the insertion slot could never move off the
    /// item's own position, which made the drop a no-op in one direction and unreachable in the
    /// other.
    /// </summary>
    private List<(int Index, Rect Bounds)> SettledContainerBounds()
    {
        var bounds = new List<(int Index, Rect Bounds)>();
        if (_owner is null || _topLevel is null)
        {
            return bounds;
        }

        foreach (var container in _owner.GetRealizedContainers())
        {
            var index = _owner.IndexFromContainer(container);
            if (index < 0
                || ReferenceEquals(container, _draggedVisual)
                || container.TranslatePoint(default, _topLevel) is not { } topLeft)
            {
                continue;
            }

            bounds.Add((index, new Rect(topLeft, container.Bounds.Size)));
        }

        bounds.Sort((left, right) => left.Index.CompareTo(right.Index));
        return bounds;
    }

    /// <summary>
    /// Whether items run down the screen (the page-tile list) rather than across it (the pad grid).
    /// Measured from where consecutive containers actually sit, so it needs no knowledge of which
    /// panel is in use.
    /// </summary>
    private static bool IsVerticallyStacked(List<(int Index, Rect Bounds)> containers)
    {
        if (containers.Count < 2)
        {
            return true;
        }

        var first = containers[0].Bounds;
        var second = containers[1].Bounds;
        return Math.Abs(second.Y - first.Y) > Math.Abs(second.X - first.X);
    }

    private static (int Index, Rect Bounds) NearestContainer(List<(int Index, Rect Bounds)> containers, Point point)
    {
        var nearest = containers[0];
        var shortest = double.MaxValue;

        foreach (var candidate in containers)
        {
            if (candidate.Bounds.Contains(point))
            {
                return candidate;
            }

            var distance = ((Vector)(candidate.Bounds.Center - point)).Length;
            if (distance < shortest)
            {
                shortest = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Draws the drop line on the TopLevel's overlay layer, which is a plain Canvas covering the
    /// window — so the line can be placed in the same coordinates the containers were measured in,
    /// and is never clipped by the list it belongs to.
    /// </summary>
    private void ShowIndicator(List<(int Index, Rect Bounds)> containers, bool stacked)
    {
        if (IndicatorRect(containers, stacked) is not { } rect)
        {
            return;
        }

        _indicatorLayer ??= _owner is null ? null : OverlayLayer.GetOverlayLayer(_owner);
        if (_indicatorLayer is null)
        {
            return;
        }

        if (_indicator is null)
        {
            // Every item has its own behaviour instance but they all share one overlay layer, so a
            // line left behind by a drag that ended badly would otherwise sit there for the rest of
            // the session — and a second attempt would simply add another. Clearing tagged
            // leftovers keeps at most one drop line on screen no matter what went wrong before.
            RemoveStaleIndicators(_indicatorLayer);

            _indicator = new Border
            {
                Tag = IndicatorTag,
                CornerRadius = new CornerRadius(IndicatorThickness / 2),
                IsHitTestVisible = false,
                Background = _item.FindResource("DropIndicatorBrush") as IBrush ?? Brushes.DodgerBlue,
            };

            _indicatorLayer.Children.Add(_indicator);
        }

        _indicator.Width = rect.Width;
        _indicator.Height = rect.Height;
        Canvas.SetLeft(_indicator, rect.X);
        Canvas.SetTop(_indicator, rect.Y);
    }

    /// <summary>
    /// The line's rectangle for the current insertion gap: along the leading edge of the item that
    /// would be pushed along, or the trailing edge of the last item when dropping at the end.
    /// </summary>
    private Rect? IndicatorRect(List<(int Index, Rect Bounds)> containers, bool stacked)
    {
        var reference = containers[^1];
        var atEnd = true;

        // The first item at or past the gap is the one that would be pushed along, so the line goes
        // on its leading edge. It's ">=" rather than "==" because the dragged item's own index is
        // missing from this list: when the gap is the item's current position, the item that would
        // be pushed is the one after it.
        foreach (var candidate in containers)
        {
            if (candidate.Index >= _insertSlot)
            {
                reference = candidate;
                atEnd = false;
                break;
            }
        }

        var bounds = reference.Bounds;
        var offset = IndicatorThickness / 2;

        return stacked
            ? new Rect(bounds.X, (atEnd ? bounds.Bottom : bounds.Y) - offset, bounds.Width, IndicatorThickness)
            : new Rect((atEnd ? bounds.Right : bounds.X) - offset, bounds.Y, IndicatorThickness, bounds.Height);
    }

    private static void RemoveStaleIndicators(OverlayLayer layer)
    {
        for (var i = layer.Children.Count - 1; i >= 0; i--)
        {
            if (layer.Children[i] is Border { Tag: IndicatorTag })
            {
                layer.Children.RemoveAt(i);
            }
        }
    }

    private void HideIndicator()
    {
        if (_indicator is not null)
        {
            _indicatorLayer?.Children.Remove(_indicator);
            _indicator = null;
        }

        _indicatorLayer = null;
    }

    /// <summary>
    /// Holds the surrounding ScrollViewer still for the duration of the drag, by putting back any
    /// offset change it makes. Page tiles live in a ListBox, so without this the scroll gesture
    /// pans the list out from under the finger mid-drag.
    ///
    /// The offset is pinned rather than scrolling being switched off: setting
    /// <see cref="ScrollBarVisibility.Disabled"/>, which this replaced, re-measures the content
    /// against the viewport instead of its natural size and forces the offset to zero — so on any
    /// list long enough to have been scrolled, the whole thing jumped the instant a drag began.
    /// </summary>
    private void SuppressAncestorScrolling()
    {
        if (_item.FindAncestorOfType<ScrollViewer>() is not { } scroll)
        {
            return;
        }

        _pinnedScroll = scroll;
        _pinnedOffset = scroll.Offset;
        scroll.ScrollChanged += OnPinnedScrollChanged;
    }

    /// <summary>Puts back an offset the ScrollViewer changed mid-drag. Assigning the pinned value
    /// re-raises this, which then matches and stops — so there's no loop to guard against.</summary>
    private void OnPinnedScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_pinnedScroll is { } scroll && scroll.Offset != _pinnedOffset)
        {
            scroll.Offset = _pinnedOffset;
        }
    }

    private void RestoreAncestorScrolling()
    {
        if (_pinnedScroll is { } scroll)
        {
            scroll.ScrollChanged -= OnPinnedScrollChanged;
            _pinnedScroll = null;
        }
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
