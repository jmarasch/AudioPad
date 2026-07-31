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
        _ = new HoldDragReorderBehavior<PageViewModel>(RootBorder, OnMoveRequested);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PageViewModel page
            && VisualTreeHelpers.FindAncestorDataContext<MainWindowViewModel>(RootBorder) is { } mainWindow)
        {
            mainWindow.NavigateToPageCommand.Execute(page);
        }
    }

    /// <summary>
    /// The owning view model is supplied by the drag behaviour, captured when the drag began —
    /// see the matching note in <see cref="PadButton"/> for why it can't be resolved mid-drag.
    /// </summary>
    private void OnMoveRequested(object? owner, PageViewModel source, PageViewModel target)
    {
        (owner as MainWindowViewModel)?.MovePage(source, target);
    }
}
