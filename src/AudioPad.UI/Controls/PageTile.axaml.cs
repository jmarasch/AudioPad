using Avalonia.Controls;
using Avalonia.Input;
using AudioPad.UI.Interactions;
using AudioPad.UI.ViewModels;

namespace AudioPad.UI.Controls;

public partial class PageTile : UserControl
{
    public PageTile()
    {
        InitializeComponent();
        _ = new HoldDragReorderBehavior<PageViewModel>(RootBorder, OnSwapRequested);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PageViewModel page
            && VisualTreeHelpers.FindAncestorDataContext<MainWindowViewModel>(RootBorder) is { } mainWindow)
        {
            mainWindow.NavigateToPageCommand.Execute(page);
        }
    }

    private void OnSwapRequested(PageViewModel source, PageViewModel target)
    {
        VisualTreeHelpers.FindAncestorDataContext<MainWindowViewModel>(RootBorder)?.SwapPages(source, target);
    }
}
