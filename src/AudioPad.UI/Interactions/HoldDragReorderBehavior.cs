using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AudioPad.UI.Interactions;

/// <summary>
/// Attaches "hold, then drag to a new spot, swap in place" reordering to one item in a same-type
/// collection (pads in a page's grid, page tiles in the overview). Built on Avalonia's native
/// long-press gesture (<see cref="InputElement.HoldingEvent"/>, with mouse-holding enabled too, so
/// it's testable on Desktop) rather than <c>Avalonia.Input.DragDrop</c>, since it needs full
/// control over the in-place drag visual (a plain <see cref="TranslateTransform"/> follow) instead
/// of an OS-level drag cursor/drop-effect, and needs to work identically for a fixed grid of pads
/// and a reflowing wrap of page tiles via the same hit-testing approach.
/// </summary>
/// <typeparam name="TItem">The DataContext type of one reorderable item (e.g. PadViewModel).</typeparam>
public sealed class HoldDragReorderBehavior<TItem>
    where TItem : class
{
    private readonly InputElement _item;
    private readonly Action<TItem, TItem> _onSwap;
    private readonly TranslateTransform _transform = new();

    private TopLevel? _topLevel;
    private Point? _lastPosition;
    private bool _dragging;

    public HoldDragReorderBehavior(InputElement item, Action<TItem, TItem> onSwap)
    {
        _item = item;
        _onSwap = onSwap;

        InputElement.SetIsHoldingEnabled(item, true);
        InputElement.SetIsHoldWithMouseEnabled(item, true);
        item.RenderTransform = _transform;

        // The pointer is captured on every press (harmless for a plain tap/click) so that once a
        // hold is recognized, PointerMoved keeps arriving even after the pointer leaves the
        // item's own bounds — required for dragging it over a distant sibling. Registered with
        // handledEventsToo: true because Button marks its own PointerPressed/Released handling as
        // Handled (for its Click/pressed-state bookkeeping), which would otherwise stop a plain
        // `+=` subscription — a bubble-only, not-handled-events instance handler — from ever firing.
        item.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, handledEventsToo: true);
        item.Holding += OnHolding;
        item.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, handledEventsToo: true);
        item.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
        item.PointerCaptureLost += OnPointerCaptureLost;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _topLevel = TopLevel.GetTopLevel(_item);
        _lastPosition = null;
        e.Pointer.Capture(_item);
    }

    private void OnHolding(object? sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState != HoldingState.Started)
        {
            return;
        }

        _dragging = true;
        _item.ZIndex = 100;
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _topLevel is null)
        {
            return;
        }

        var position = e.GetPosition(_topLevel);
        if (_lastPosition is { } last)
        {
            _transform.X += position.X - last.X;
            _transform.Y += position.Y - last.Y;
        }

        _lastPosition = position;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        EndDrag(_dragging && _topLevel is not null ? e.GetPosition(_topLevel) : null);

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndDrag(null);

    private void EndDrag(Point? dropPoint)
    {
        var wasDragging = _dragging;
        _dragging = false;
        _transform.X = 0;
        _transform.Y = 0;
        _item.ZIndex = 0;

        if (wasDragging && dropPoint is { } point && _item.DataContext is TItem source)
        {
            var target = HitTestItem(point);
            if (target is not null && !ReferenceEquals(target, source))
            {
                _onSwap(source, target);
            }
        }
    }

    private TItem? HitTestItem(Point point)
    {
        if (_topLevel is null)
        {
            return null;
        }

        foreach (var visual in _topLevel.GetVisualsAt(point))
        {
            if (VisualTreeHelpers.FindAncestorDataContext<TItem>(visual) is { } item)
            {
                return item;
            }
        }

        return null;
    }
}
