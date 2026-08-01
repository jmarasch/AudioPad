using AudioPad.Core.Models;
using AudioPad.Core.Persistence;
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

    /// <summary>
    /// What the pad pointed at when the dialog opened. Kept so that saving can delete a library
    /// file the pad no longer uses — replaced by a different import, or cleared outright.
    /// </summary>
    private readonly string? _originalAudioPath;
    private readonly string? _originalIconPath;

    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private string? _audioFilePath;

    /// <summary>The clip's original name, which is what gets shown — the stored file is a GUID.</summary>
    [ObservableProperty]
    private string? _audioDisplayName;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private string? _iconDisplayName;

    [ObservableProperty]
    private PlaybackMode _mode;

    [ObservableProperty]
    private float _volume;

    [ObservableProperty]
    private PadColorChoice _inactiveColor;

    [ObservableProperty]
    private PadColorChoice _activeColor;

    [ObservableProperty]
    private PadColorChoice _inactiveHoverColor;

    [ObservableProperty]
    private PadColorChoice _activeHoverColor;

    public IReadOnlyList<PlaybackMode> AvailableModes { get; } = Enum.GetValues<PlaybackMode>();

    /// <summary>The clip's name, which is what the user chose — a full path says nothing useful
    /// now that the file lives in the app's own library.</summary>
    public string AudioFileName => AudioDisplayName ?? "No audio imported";

    public string IconFileName => IconDisplayName ?? "No icon imported";

    /// <summary>
    /// Colour options for each of the four states. Each list leads with "Use page default", so a
    /// pad only overrides what it's actually been asked to override.
    /// </summary>
    public IReadOnlyList<PadColorChoice> InactiveChoices { get; }

    public IReadOnlyList<PadColorChoice> ActiveChoices { get; }

    public IReadOnlyList<PadColorChoice> InactiveHoverChoices { get; }

    public IReadOnlyList<PadColorChoice> ActiveHoverChoices { get; }

    public PadConfigViewModel(PadConfig target, Action<bool> onClose)
    {
        _target = target;
        _onClose = onClose;

        _label = target.Label;
        _audioFilePath = target.AudioFilePath;
        _iconPath = target.IconPath;

        // Pads saved before names were stored fall back to the file's own name, which for those is
        // still the original one.
        _audioDisplayName = target.AudioFileName ?? NameFromPath(target.AudioFilePath);
        _iconDisplayName = target.IconFileName ?? NameFromPath(target.IconPath);
        _mode = target.Mode;
        _volume = target.Volume;
        _originalAudioPath = target.AudioFilePath;
        _originalIconPath = target.IconPath;

        const string inherit = "Use page default";
        InactiveChoices = PadColorPresets.BuildChoices(PadColorPresets.All, target.Colors.Inactive, inherit);
        ActiveChoices = PadColorPresets.BuildChoices(PadColorPresets.All, target.Colors.Active, inherit);
        InactiveHoverChoices = PadColorPresets.BuildChoices(PadColorPresets.All, target.Colors.InactiveHover, inherit);
        ActiveHoverChoices = PadColorPresets.BuildChoices(PadColorPresets.All, target.Colors.ActiveHover, inherit);

        _inactiveColor = PadColorPresets.Select(InactiveChoices, target.Colors.Inactive);
        _activeColor = PadColorPresets.Select(ActiveChoices, target.Colors.Active);
        _inactiveHoverColor = PadColorPresets.Select(InactiveHoverChoices, target.Colors.InactiveHover);
        _activeHoverColor = PadColorPresets.Select(ActiveHoverChoices, target.Colors.ActiveHover);
    }

    partial void OnAudioDisplayNameChanged(string? value) => OnPropertyChanged(nameof(AudioFileName));

    partial void OnIconDisplayNameChanged(string? value) => OnPropertyChanged(nameof(IconFileName));

    private static string? NameFromPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path);

    [RelayCommand]
    private void Save()
    {
        // A library file the pad has stopped using is now unreachable, so it goes with it. Only
        // ever files the library owns — see MediaLibrary.Delete.
        if (_originalAudioPath != AudioFilePath)
        {
            MediaLibrary.Delete(_originalAudioPath);
        }

        if (_originalIconPath != IconPath)
        {
            MediaLibrary.Delete(_originalIconPath);
        }

        _target.Label = Label;
        _target.AudioFilePath = AudioFilePath;
        _target.AudioFileName = AudioDisplayName;
        _target.IconPath = IconPath;
        _target.IconFileName = IconDisplayName;
        _target.Mode = Mode;
        _target.Volume = Volume;
        _target.Colors = new PadPalette
        {
            Inactive = InactiveColor.Value,
            Active = ActiveColor.Value,
            InactiveHover = InactiveHoverColor.Value,
            ActiveHover = ActiveHoverColor.Value,
        };

        _onClose(true);
    }

    /// <summary>
    /// Returns the pad to being unconfigured — no clip, no icon, no label, no colour overrides —
    /// and commits it, closing the dialog. Goes through <see cref="Save"/> so there is one path
    /// that writes to the pad, rather than a second one that could drift from it.
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        Label = string.Empty;
        AudioFilePath = null;
        AudioDisplayName = null;
        IconPath = null;
        IconDisplayName = null;
        Mode = PlaybackMode.Latch;
        Volume = 1.0f;
        InactiveColor = InactiveChoices[0];
        ActiveColor = ActiveChoices[0];
        InactiveHoverColor = InactiveHoverChoices[0];
        ActiveHoverColor = ActiveHoverChoices[0];

        Save();
    }

    [RelayCommand]
    private void Cancel() => _onClose(false);
}
