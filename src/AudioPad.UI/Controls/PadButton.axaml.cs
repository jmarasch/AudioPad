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
        _ = new HoldDragReorderBehavior<PadViewModel>(RootButton, OnDropped);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PadViewModel pad)
        {
            pad.OpenConfigCommand.Execute(null);
        }
    }

    /// <summary>
    /// The owning page is supplied by the drag behaviour, captured when the drag began. It can't
    /// be looked up from this control mid-drag: reordering recycles containers and detaches it
    /// from the visual tree, so the lookup would return null after the first move.
    /// </summary>
    private void OnDropped(object? owner, PadViewModel pad, int index)
    {
        (owner as PageViewModel)?.MovePad(pad, index);
    }
}
