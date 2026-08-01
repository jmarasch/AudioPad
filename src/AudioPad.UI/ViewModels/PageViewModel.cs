using System.Collections.ObjectModel;
using AudioPad.Core.Models;
using AudioPad.Core.Playback;
using Avalonia.Media;

namespace AudioPad.UI.ViewModels;

/// <summary>Wraps one <see cref="Page"/> with its pads and display state, for the carousel/grid to bind to.</summary>
public sealed class PageViewModel : ViewModelBase
{
    private readonly IAudioEngine _audioEngine;
    private readonly Action<PadViewModel> _onPadConfigRequested;
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
        _audioEngine = audioEngine;
        _onPadConfigRequested = onPadConfigRequested;
        _onChanged = onChanged;

        Pads = new ObservableCollection<PadViewModel>();
        SyncPadsWithPage();
    }

    /// <summary>
    /// Re-reads display state from <see cref="Page"/> after its settings have been edited and
    /// saved, rebuilding the pad list too since those settings include the grid size.
    /// </summary>
    public void RefreshFromPage()
    {
        SyncPadsWithPage();

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ThemeBrush));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Columns));
    }

    /// <summary>
    /// Detaches every pad from the audio engine, for a page discarded wholesale — as happens when an
    /// imported setup replaces the current one. The engine outlives the pages, so without this the
    /// discarded view models stay alive and keep reacting to playback.
    /// </summary>
    public void Detach()
    {
        foreach (var pad in Pads)
        {
            pad.ConfigRequested -= _onPadConfigRequested;
            pad.Detach();
        }
    }

    /// <summary>
    /// Brings <see cref="Pads"/> back in line with <see cref="Page"/>'s pads after a resize. Pads
    /// that survived keep their existing view model, so one that's mid-playback keeps its lit
    /// state; the ones that didn't are detached so they stop listening to the audio engine.
    /// </summary>
    private void SyncPadsWithPage()
    {
        var existing = Pads.ToDictionary(pad => pad.Config.Id);
        var synced = new List<PadViewModel>(Page.Pads.Count);

        foreach (var padConfig in Page.Pads)
        {
            if (existing.Remove(padConfig.Id, out var pad))
            {
                synced.Add(pad);
                continue;
            }

            pad = new PadViewModel(padConfig, _audioEngine);
            pad.ConfigRequested += _onPadConfigRequested;
            synced.Add(pad);
        }

        foreach (var dropped in existing.Values)
        {
            dropped.ConfigRequested -= _onPadConfigRequested;
            dropped.Detach();
        }

        Pads.Clear();
        foreach (var pad in synced)
        {
            Pads.Add(pad);
        }
    }

    /// <summary>
    /// Moves a pad to a new index, sliding everything between along to make room — reordering a
    /// list, not swapping a pair. Dragging pad 1 to pad 5's place leaves 2-5 shifted back by one
    /// rather than dumping pad 5 into the vacated slot, which is what "rearranging notes on a page"
    /// does and what makes a run of related pads stay in order.
    ///
    /// Called once per drag, when the finger lifts, so saving here costs one write per gesture.
    /// </summary>
    public void MovePad(PadViewModel pad, int index)
    {
        var from = Pads.IndexOf(pad);
        var to = Math.Clamp(index, 0, Pads.Count - 1);
        if (from < 0 || from == to)
        {
            return;
        }

        Pads.Move(from, to);

        Page.Pads.RemoveAt(from);
        Page.Pads.Insert(to, pad.Config);
        ReassignGridPositions();

        _onChanged();
    }

    /// <summary>
    /// Re-derives every pad's Row/Column from its place in the list. Collection order is what the
    /// UniformGrid actually lays out, so after a reorder the stored coordinates have to be brought
    /// back in line with it — otherwise a later resize (which reads Row/Column) would scatter them.
    /// </summary>
    private void ReassignGridPositions()
    {
        for (var index = 0; index < Page.Pads.Count; index++)
        {
            Page.Pads[index].Row = index / Page.Columns;
            Page.Pads[index].Column = index % Page.Columns;
        }
    }
}
