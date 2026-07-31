using AudioPad.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPad.UI.ViewModels;

/// <summary>
/// Editable working copy of one pad's configuration. Changes only reach the underlying
/// <see cref="PadConfig"/> on <see cref="SaveCommand"/>, so cancelling is a true no-op.
/// </summary>
public sealed partial class PadConfigViewModel : ViewModelBase
{
    private readonly PadConfig _target;
    private readonly Action<bool> _onClose;

    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private string? _audioFilePath;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private PlaybackMode _mode;

    [ObservableProperty]
    private float _volume;

    public IReadOnlyList<PlaybackMode> AvailableModes { get; } = Enum.GetValues<PlaybackMode>();

    public PadConfigViewModel(PadConfig target, Action<bool> onClose)
    {
        _target = target;
        _onClose = onClose;

        _label = target.Label;
        _audioFilePath = target.AudioFilePath;
        _iconPath = target.IconPath;
        _mode = target.Mode;
        _volume = target.Volume;
    }

    [RelayCommand]
    private void Save()
    {
        _target.Label = Label;
        _target.AudioFilePath = AudioFilePath;
        _target.IconPath = IconPath;
        _target.Mode = Mode;
        _target.Volume = Volume;
        _onClose(true);
    }

    [RelayCommand]
    private void Cancel() => _onClose(false);
}
