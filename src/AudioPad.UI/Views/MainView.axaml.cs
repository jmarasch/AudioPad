using AudioPad.UI.Interactions;
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
    /// A pinch reports continuously, so without this one gesture would fire the command on every
    /// update. Cleared when the gesture ends.
    /// </summary>
    private bool _pinchActioned;

    public MainView()
    {
        InitializeComponent();
        PageCarousel.SelectionChanged += OnCarouselSelectionChanged;

        // A gesture recogniser rather than the hand-rolled two-pointer tracking this replaced:
        // that version listened on the same raw pointer events the pads' hold-drag uses, and the
        // two gestures fought over them. A recogniser owns its pointers through Avalonia's own
        // arbitration instead, so the drag gives way cleanly when a pinch takes over.
        //
        // See TwoFingerPinchRecognizer for why it isn't Avalonia's built-in PinchGestureRecognizer:
        // that one cancels the Carousel's swipe on every single-finger touch.
        var pinch = new TwoFingerPinchRecognizer();
        pinch.Pinched += OnPinched;
        pinch.PinchFinished += OnPinchFinished;
        GestureRecognizers.Add(pinch);
    }

    /// <summary>
    /// Zooms out to the overview once the fingers have closed far enough, and back into the page
    /// once they've spread far enough. <paramref name="scale"/> is measured against the finger
    /// distance at the start of the gesture, so it reads as a plain zoom factor.
    /// </summary>
    private void OnPinched(double scale)
    {
        GestureLog.Write($"pinch scale={scale:F2} actioned={_pinchActioned}");

        if (_pinchActioned || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

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

    private void OnPinchFinished()
    {
        GestureLog.Write("pinch finished");
        _pinchActioned = false;
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
        GestureLog.Write(
            $"carousel selection {index} (vm {viewModel.SelectedCarouselIndex}) swipe={PageCarousel.IsSwipeEnabled}");

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
