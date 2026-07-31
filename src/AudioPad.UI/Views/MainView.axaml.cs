using AudioPad.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace AudioPad.UI.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        PageCarousel.SelectionChanged += OnCarouselSelectionChanged;
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
