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

    public IReadOnlyList<int> AvailableGridSizes { get; } = GridSizes;

    public IReadOnlyList<string> AvailableThemeColors { get; }

    /// <summary>
    /// Whether the pending size would drop cells the page currently has, so the view can warn
    /// that shrinking discards whatever is configured on those pads.
    /// </summary>
    public bool IsShrinking => Rows < _target.Rows || Columns < _target.Columns;

    public PageConfigViewModel(Page target, Action<bool> onClose)
    {
        _target = target;
        _onClose = onClose;

        _title = target.Title;
        _themeColor = target.ThemeColor;
        _rows = target.Rows;
        _columns = target.Columns;

        // A page saved with a color that's since been dropped from the presets would otherwise
        // bind to nothing and get wiped on save, so keep its current one selectable.
        AvailableThemeColors = PresetThemeColors.Contains(target.ThemeColor)
            ? PresetThemeColors
            : [target.ThemeColor, .. PresetThemeColors];
    }

    [RelayCommand]
    private void Save()
    {
        _target.Title = string.IsNullOrWhiteSpace(Title) ? _target.Title : Title.Trim();
        _target.ThemeColor = ThemeColor;
        _target.Resize(Rows, Columns);
        _onClose(true);
    }

    [RelayCommand]
    private void Cancel() => _onClose(false);

    partial void OnRowsChanged(int value) => OnPropertyChanged(nameof(IsShrinking));

    partial void OnColumnsChanged(int value) => OnPropertyChanged(nameof(IsShrinking));
}
