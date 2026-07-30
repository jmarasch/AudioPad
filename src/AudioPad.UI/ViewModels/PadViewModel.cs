using AudioPad.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPad.UI.ViewModels;

/// <summary>Wraps a <see cref="PadConfig"/> with the UI-facing "lit while playing" state for one grid button.</summary>
public sealed partial class PadViewModel : ViewModelBase
{
    public PadConfig Config { get; }

    [ObservableProperty]
    private bool _isLit;

    public PadViewModel(PadConfig config)
    {
        Config = config;
    }

    public string Label => Config.Label;

    /// <summary>
    /// Placeholder for the real pad-press behavior: toggles the lit indicator locally so the grid
    /// layout and styling can be seen working. Wiring this to
    /// <see cref="AudioPad.Core.Playback.IAudioEngine"/> for real Latch/Loop playback — and adding
    /// double-tap to open the per-pad config dialog — is the next milestone (see docs/ARCHITECTURE.md).
    /// </summary>
    [RelayCommand]
    private void Toggle()
    {
        IsLit = !IsLit;
    }
}
