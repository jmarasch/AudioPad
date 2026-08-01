using AudioPad.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPad.UI.ViewModels;

/// <summary>
/// Editable working copy of one page's settings. Changes only reach the underlying
/// <see cref="Page"/> on <see cref="SaveCommand"/>, so cancelling is a true no-op — matching
/// <see cref="PadConfigViewModel"/>'s behaviour for a single pad.
/// </summary>
public sealed partial class PageConfigViewModel : ViewModelBase
{
    /// <summary>Grid dimensions offered in the UI. A fixed list, so an unusable size can't be entered.</summary>
    private static readonly int[] GridSizes = Enumerable.Range(1, 8).ToArray();

    /// <summary>Preset header colors, so a page can't end up with a <see cref="Page.ThemeColor"/>
    /// that <c>Color.Parse</c> would later throw on.</summary>
    private static readonly string[] PresetThemeColors =
    [
        "#FF7043", "#EF5350", "#AB47BC", "#5C6BC0",
        "#42A5F5", "#26A69A", "#66BB6A", "#FFA726",
        "#8D6E63", "#78909C",
    ];

    private readonly Page _target;
    private readonly Action<bool> _onClose;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _themeColor;

    [ObservableProperty]
    private int _rows;

    [ObservableProperty]
    private int _columns;

    [ObservableProperty]
    private PadColorChoice _padInactiveColor;

    [ObservableProperty]
    private PadColorChoice _padActiveColor;

    [ObservableProperty]
    private PadColorChoice _padInactiveHoverColor;

    [ObservableProperty]
    private PadColorChoice _padActiveHoverColor;

    /// <summary>Whether playing pads on this page show the restart/stop/pause zones and time.</summary>
    [ObservableProperty]
    private bool _showPadControls;

    public IReadOnlyList<int> AvailableGridSizes { get; } = GridSizes;

    public IReadOnlyList<string> AvailableThemeColors { get; }

    /// <summary>
    /// Pad colour options for the page. Unlike a pad's own pickers these have no "inherit" entry:
    /// the page is where the fallback stops, so every one of them names a real colour.
    /// </summary>
    public IReadOnlyList<PadColorChoice> PadInactiveChoices { get; }

    public IReadOnlyList<PadColorChoice> PadActiveChoices { get; }

    public IReadOnlyList<PadColorChoice> PadInactiveHoverChoices { get; }

    public IReadOnlyList<PadColorChoice> PadActiveHoverChoices { get; }

    /// <summary>
    /// Whether the pending size would drop cells the page currently has, so the view can warn
    /// that shrinking discards whatever is configured on those pads.
    /// </summary>
    public bool IsShrinking => Rows < _target.Rows || Columns < _target.Columns;

    /// <summary>
    /// Whether the chosen grid is dense enough that the on-pad controls are worth a word of
    /// warning. It only advises — whether they're actually awkward depends on the screen, and a
    /// dense grid that's fiddly on a phone is fine on a desktop monitor, so the choice stays with
    /// the user rather than being taken away by a threshold.
    /// </summary>
    public bool IsGridTightForControls => ShowPadControls && (Rows >= 6 || Columns >= 6);

    public PageConfigViewModel(Page target, Action<bool> onClose)
    {
        _target = target;
        _onClose = onClose;

        _title = target.Title;
        _themeColor = target.ThemeColor;
        _rows = target.Rows;
        _columns = target.Columns;
        _showPadControls = target.ShowPadControls;

        // A page saved with a color that's since been dropped from the presets would otherwise
        // bind to nothing and get wiped on save, so keep its current one selectable.
        AvailableThemeColors = PresetThemeColors.Contains(target.ThemeColor)
            ? PresetThemeColors
            : [target.ThemeColor, .. PresetThemeColors];

        // Pages saved before pad colouring existed have nothing set, so the pickers open showing
        // the built-in look rather than a blank.
        var defaults = PadPaletteResolver.Resolve(null, target.PadColors);

        PadInactiveChoices = PadColorPresets.BuildChoices(PadColorPresets.All, defaults.Inactive, inheritLabel: null);
        PadActiveChoices = PadColorPresets.BuildChoices(PadColorPresets.All, defaults.Active, inheritLabel: null);
        PadInactiveHoverChoices = PadColorPresets.BuildChoices(PadColorPresets.All, defaults.InactiveHover, inheritLabel: null);
        PadActiveHoverChoices = PadColorPresets.BuildChoices(PadColorPresets.All, defaults.ActiveHover, inheritLabel: null);

        _padInactiveColor = PadColorPresets.Select(PadInactiveChoices, defaults.Inactive);
        _padActiveColor = PadColorPresets.Select(PadActiveChoices, defaults.Active);
        _padInactiveHoverColor = PadColorPresets.Select(PadInactiveHoverChoices, defaults.InactiveHover);
        _padActiveHoverColor = PadColorPresets.Select(PadActiveHoverChoices, defaults.ActiveHover);
    }

    [RelayCommand]
    private void Save()
    {
        _target.Title = string.IsNullOrWhiteSpace(Title) ? _target.Title : Title.Trim();
        _target.ThemeColor = ThemeColor;
        _target.PadColors = new PadPalette
        {
            Inactive = PadInactiveColor.Value,
            Active = PadActiveColor.Value,
            InactiveHover = PadInactiveHoverColor.Value,
            ActiveHover = PadActiveHoverColor.Value,
        };

        _target.ShowPadControls = ShowPadControls;
        _target.Resize(Rows, Columns);
        _onClose(true);
    }

    /// <summary>
    /// Puts the four pad colours back to the built-in look. Unlike the pad dialog's Clear this
    /// only changes the pickers — the page has other settings in the same dialog, and resetting
    /// colours shouldn't commit a half-finished grid resize along with them.
    /// </summary>
    [RelayCommand]
    private void ResetPadColors()
    {
        var defaults = PadPaletteResolver.CreateDefaults();

        PadInactiveColor = PadColorPresets.Select(PadInactiveChoices, defaults.Inactive);
        PadActiveColor = PadColorPresets.Select(PadActiveChoices, defaults.Active);
        PadInactiveHoverColor = PadColorPresets.Select(PadInactiveHoverChoices, defaults.InactiveHover);
        PadActiveHoverColor = PadColorPresets.Select(PadActiveHoverChoices, defaults.ActiveHover);
    }

    [RelayCommand]
    private void Cancel() => _onClose(false);

    partial void OnRowsChanged(int value) => OnGridChanged();

    partial void OnColumnsChanged(int value) => OnGridChanged();

    partial void OnShowPadControlsChanged(bool value) => OnPropertyChanged(nameof(IsGridTightForControls));

    private void OnGridChanged()
    {
        OnPropertyChanged(nameof(IsShrinking));
        OnPropertyChanged(nameof(IsGridTightForControls));
    }
}
