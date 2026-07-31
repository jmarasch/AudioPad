using AudioPad.UI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AudioPad.UI.Views;

public partial class MainView : UserControl
{
    /// <summary>How far the fingers must close before a pinch counts as "zoom out to the overview".</summary>
    private const double PinchInThreshold = 0.75;

    /// <summary>How far they must spread before a pinch counts as "zoom back into the page".</summary>
    private const double PinchOutThreshold = 1.35;

    /// <summary>
    /// Live touch points, so a two-finger pinch can be measured directly. Avalonia's own pinch
    /// gesture type isn't accessible from application code in this version, and tracking the two
    /// pointers is simple enough that adopting it isn't worth waiting for.
    /// </summary>
    private readonly Dictionary<int, Point> _touchPoints = [];

    private double _pinchStartDistance;

    /// <summary>
    /// A pinch reports continuously, so without this one gesture would fire the command on every
    /// update. Cleared when a finger lifts.
    /// </summary>
    private bool _pinchActioned;

    public MainView()
    {
        InitializeComponent();
        PageCarousel.SelectionChanged += OnCarouselSelectionChanged;

        // Pinch handlers deliberately not wired: tunnelling pointer handlers here appeared to
        // interfere with the drag-reorder gesture, and the pinch itself never fired on-device.
        // Needs diagnosing on the tablet before being re-enabled.
    }

    private void OnPinchPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _touchPoints[e.Pointer.Id] = e.GetPosition(this);
        if (_touchPoints.Count == 2)
        {
            _pinchStartDistance = CurrentTouchDistance();
            _pinchActioned = false;
        }
    }

    private void OnPinchPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_touchPoints.ContainsKey(e.Pointer.Id))
        {
            return;
        }

        _touchPoints[e.Pointer.Id] = e.GetPosition(this);

        if (_touchPoints.Count != 2 || _pinchActioned || _pinchStartDistance <= 0
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var scale = CurrentTouchDistance() / _pinchStartDistance;

        if (!viewModel.IsPageOverviewOpen && scale <= PinchInThreshold)
        {
            viewModel.OpenPageOverviewCommand.Execute(null);
            _pinchActioned = true;
        }
        else if (viewModel.IsPageOverviewOpen && scale >= PinchOutThreshold)
        {
            viewModel.CloseOverviewCommand.Execute(null);
            _pinchActioned = true;
        }
    }

    private void OnPinchPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _touchPoints.Remove(e.Pointer.Id);
        if (_touchPoints.Count < 2)
        {
            _pinchStartDistance = 0;
            _pinchActioned = false;
        }
    }

    private double CurrentTouchDistance()
    {
        var points = _touchPoints.Values.ToList();
        return points.Count < 2 ? 0 : Math.Sqrt(
            Math.Pow(points[1].X - points[0].X, 2) + Math.Pow(points[1].Y - points[0].Y, 2));
    }

    /// <summary>
    /// Swiping/advancing onto one of the sentinel clones at either end of
    /// <see cref="MainWindowViewModel.CarouselItems"/> should look like it continued smoothly
    /// into the real page it stands in for. Silently (no animation) snap the selection to that
    /// real index right after landing on the clone, so the next user-initiated move still
    /// animates normally.
    /// </summary>
    private void OnCarouselSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var index = PageCarousel.SelectedIndex;
        if (!viewModel.IsSentinelIndex(index))
        {
            return;
        }

        var transition = PageCarousel.PageTransition;
        PageCarousel.PageTransition = null;
        viewModel.SelectedCarouselIndex = viewModel.ResolveSentinelIndex(index);
        Dispatcher.UIThread.Post(() => PageCarousel.PageTransition = transition);
    }
}
