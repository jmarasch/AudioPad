using Avalonia.Controls;
using Avalonia.Input;
using AudioPad.UI.Interactions;
using AudioPad.UI.ViewModels;

namespace AudioPad.UI.Controls;

public partial class PadButton : UserControl
{
    public PadButton()
    {
        InitializeComponent();
        _ = new HoldDragReorderBehavior<PadViewModel>(RootButton, OnSwapRequested);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PadViewModel pad)
        {
            pad.OpenConfigCommand.Execute(null);
        }
    }

    private void OnSwapRequested(PadViewModel source, PadViewModel target)
    {
        VisualTreeHelpers.FindAncestorDataContext<PageViewModel>(RootButton)?.SwapPads(source, target);
    }
}
