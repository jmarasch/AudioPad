using System.Collections.ObjectModel;
using AudioPad.Core.Models;
using AudioPad.Core.Playback;
using Avalonia.Media;

namespace AudioPad.UI.ViewModels;

/// <summary>Wraps one <see cref="Page"/> with its pads and display state, for the carousel/grid to bind to.</summary>
public sealed class PageViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    public Page Page { get; }

    public string Title => Page.Title;

    public IBrush ThemeBrush => new SolidColorBrush(Color.Parse(Page.ThemeColor));

    public int Rows => Page.Rows;

    public int Columns => Page.Columns;

    public ObservableCollection<PadViewModel> Pads { get; }

    public PageViewModel(Page page, IAudioEngine audioEngine, Action<PadViewModel> onPadConfigRequested, Action onChanged)
    {
        Page = page;
        _onChanged = onChanged;

        Pads = new ObservableCollection<PadViewModel>();
        foreach (var padConfig in page.Pads)
        {
            var pad = new PadViewModel(padConfig, audioEngine);
            pad.ConfigRequested += onPadConfigRequested;
            Pads.Add(pad);
        }
    }

    /// <summary>Re-reads display state from <see cref="Page"/> after its settings have been edited and saved.</summary>
    public void RefreshFromPage()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ThemeBrush));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Columns));
    }

    /// <summary>
    /// Swaps two pads' grid positions (both their <see cref="PadConfig"/> Row/Column — the
    /// persisted, positionally-meaningful identity — and their position in <see cref="Pads"/>,
    /// since the UniformGrid lays items out in collection order, not by reading Row/Column).
    /// </summary>
    public void SwapPads(PadViewModel a, PadViewModel b)
    {
        var indexA = Pads.IndexOf(a);
        var indexB = Pads.IndexOf(b);
        if (indexA < 0 || indexB < 0 || indexA == indexB)
        {
            return;
        }

        (a.Config.Row, b.Config.Row) = (b.Config.Row, a.Config.Row);
        (a.Config.Column, b.Config.Column) = (b.Config.Column, a.Config.Column);
        (Pads[indexA], Pads[indexB]) = (Pads[indexB], Pads[indexA]);

        _onChanged();
    }
}
