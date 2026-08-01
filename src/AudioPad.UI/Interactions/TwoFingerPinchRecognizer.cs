using Avalonia;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;

namespace AudioPad.UI.Interactions;

/// <summary>
/// Reports a two-finger pinch as a running scale factor: the current distance between the fingers
/// as a fraction of their distance when the second one landed.
///
/// This exists instead of Avalonia's own <c>PinchGestureRecognizer</c>, which cannot be used above
/// a Carousel. That one takes part in *every* touch rather than only two-fingered ones — it
/// records the first contact unconditionally, and releasing that contact calls
/// <c>PreventGestureRecognition()</c>, which sets a flag on the event that makes input routing skip
/// every other recognizer. So each ordinary one-finger tap or swipe ended by cancelling the
/// Carousel's swipe recogniser, and swipe-to-change-page stopped working the moment a pinch
/// recogniser was attached anywhere above it.
///
/// This one holds off until a second finger arrives: until then it records the first contact but
/// takes no action a single-finger gesture can observe, so a swipe or a pad's hold-drag reaches
/// its target untouched. Once two fingers are down it captures both, which is the point at which
/// stealing them from the swipe or the drag is what the user is asking for.
/// </summary>
public sealed class TwoFingerPinchRecognizer : GestureRecognizer
{
    private IPointer? _first;
    private IPointer? _second;
    private Point _firstPoint;
    private Point _secondPoint;
    private double _initialDistance;

    /// <summary>
    /// Raised as the fingers move, with the current spread as a fraction of the starting spread —
    /// below 1 they are closing, above 1 they are spreading apart.
    /// </summary>
    public event Action<double>? Pinched;

    /// <summary>Raised when a pinch that had genuinely started loses one of its two fingers.</summary>
    public event Action? PinchFinished;

    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        if (Target is not Visual target || (e.Pointer.Type != PointerType.Touch && e.Pointer.Type != PointerType.Pen))
        {
            return;
        }

        if (_first is null)
        {
            _first = e.Pointer;
            _firstPoint = e.GetPosition(target);
        }
        else if (_second is null && _first != e.Pointer)
        {
            _second = e.Pointer;
            _secondPoint = e.GetPosition(target);
            _initialDistance = Distance(_firstPoint, _secondPoint);
            StartPinch();
        }
    }

    /// <summary>
    /// Takes both fingers for the pinch. <see cref="GestureRecognizer.Capture"/> is what suppresses
    /// the other recognisers, so it is called only here — with two fingers down there is no
    /// competing single-pointer gesture left to protect.
    /// </summary>
    private void StartPinch()
    {
        if (_first is { } first && _second is { } second && _initialDistance > 0)
        {
            Capture(first);
            Capture(second);
        }
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (Target is not Visual target)
        {
            return;
        }

        if (e.Pointer == _first)
        {
            _firstPoint = e.GetPosition(target);
        }
        else if (e.Pointer == _second)
        {
            _secondPoint = e.GetPosition(target);
        }
        else
        {
            return;
        }

        if (_second is null || _initialDistance <= 0)
        {
            return;
        }

        Pinched?.Invoke(Distance(_firstPoint, _secondPoint) / _initialDistance);
        e.PreventGestureRecognition();
    }

    protected override void PointerReleased(PointerReleasedEventArgs e) => RemoveContact(e.Pointer);

    protected override void PointerCaptureLost(IPointer pointer) => RemoveContact(pointer);

    /// <summary>
    /// Drops one finger, ending any pinch in progress. Deliberately does not call
    /// <c>PreventGestureRecognition()</c>: doing so for a contact that never became part of a
    /// pinch is exactly the upstream behaviour this class exists to avoid.
    /// </summary>
    private void RemoveContact(IPointer pointer)
    {
        if (pointer != _first && pointer != _second)
        {
            return;
        }

        var wasPinching = _second is not null;

        if (pointer == _first)
        {
            _first = _second;
            _firstPoint = _secondPoint;
        }

        _second = null;
        _initialDistance = 0;

        if (wasPinching)
        {
            PinchFinished?.Invoke();
        }
    }

    private static double Distance(Point a, Point b) => new Vector(b.X - a.X, b.Y - a.Y).Length;
}
