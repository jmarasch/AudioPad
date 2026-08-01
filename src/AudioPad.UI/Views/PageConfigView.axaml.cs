using AudioPad.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace AudioPad.UI.Views;

public partial class PageConfigView : UserControl
{
    public PageConfigView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Treats a press on the dimmed area around the panel as Cancel — see the matching note in
    /// <see cref="PadConfigView"/>.
    /// </summary>
    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender) && DataContext is PageConfigViewModel viewModel)
        {
            viewModel.CancelCommand.Execute(null);
        }
    }
}
