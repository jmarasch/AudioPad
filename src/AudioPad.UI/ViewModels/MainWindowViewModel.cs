using System.Collections.ObjectModel;
using AudioPad.Core.Models;

namespace AudioPad.UI.ViewModels;

/// <summary>Owns the current grid profile and exposes it as pad view models for the UI to bind to.</summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    public int Rows { get; }

    public int Columns { get; }

    public ObservableCollection<PadViewModel> Pads { get; }

    public MainWindowViewModel()
        : this(GridProfile.CreateDefault())
    {
    }

    public MainWindowViewModel(GridProfile profile)
    {
        Rows = profile.Rows;
        Columns = profile.Columns;
        Pads = new ObservableCollection<PadViewModel>(profile.Pads.Select(pad => new PadViewModel(pad)));
    }
}
