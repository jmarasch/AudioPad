using AudioPad.Core.Models;
using AudioPad.Core.Playback;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPad.UI.ViewModels;

/// <summary>Wraps a <see cref="PadConfig"/> with the UI-facing playing/"lit" state for one grid button.</summary>
public sealed partial class PadViewModel : ViewModelBase
{
    /// <summary>
    /// How often the time readout refreshes while a pad is playing. Four times a second is fast
    /// enough that a seconds counter never looks stuck, without polling libVLC every frame.
    /// </summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    private readonly IAudioEngine _audioEngine;
    private readonly DispatcherTimer _progressTimer;

    /// <summary>The owning page, consulted for colours this pad doesn't override.</summary>
    private readonly Page _page;

    /// <summary>Shared with the whole board: whether a tap arranges rather than plays.</summary>
    private readonly EditModeState _editMode;

    public PadConfig Config { get; }

    [ObservableProperty]
    private bool _isLit;

    [ObservableProperty]
    private Bitmap? _iconBitmap;

    /// <summary>Whether playback is holding position, so the pause control can show its state.</summary>
    [ObservableProperty]
    private bool _isPaused;

    /// <summary>The time readout: elapsed of total when looping, time remaining when one-shot.</summary>
    [ObservableProperty]
    private string _timeDisplay = string.Empty;

    /// <summary>Raised when the pad is double-tapped, asking the owner to open its config dialog.</summary>
    public event Action<PadViewModel>? ConfigRequested;

    public PadViewModel(PadConfig config, Page page, IAudioEngine audioEngine, EditModeState editMode)
    {
        Config = config;
        _page = page;
        _audioEngine = audioEngine;
        _editMode = editMode;
        _editMode.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowTransport));
            OnPropertyChanged(nameof(IsEditing));
        };
        _audioEngine.PlaybackStateChanged += OnPlaybackStateChanged;
        IconBitmap = LoadIconBitmap(config.IconPath);

        _progressTimer = new DispatcherTimer { Interval = ProgressInterval };
        _progressTimer.Tick += (_, _) => RefreshProgress();
    }

    public string Label => Config.Label;

    /// <summary>The pad's configured level, shown on its face while playing.</summary>
    public string VolumePercent => $"{Math.Round(Config.Volume * 100)}%";

    /// <summary>
    /// Whether to show the transport zones and time readout: while playing, unless the page has
    /// turned them off. This used to also hide them below a measured pad size, which took the
    /// choice away on dense grids that are perfectly usable on a large screen — see
    /// <see cref="Page.ShowPadControls"/>.
    /// </summary>
    public bool ShowTransport => IsLit && _page.ShowPadControls && !_editMode.IsEditing;

    /// <summary>Whether the board is being arranged, which is what allows this pad to be dragged.</summary>
    public bool IsEditing => _editMode.IsEditing;

    /// <summary>Pause bars while playing, a play triangle once held — what pressing it does next.</summary>
    public string PauseGlyph => IsPaused ? "▶" : "⏸";

    /// <summary>The pad's colour when the pointer is elsewhere — playing or idle.</summary>
    public IBrush NormalBackground => new SolidColorBrush(StateColor(hovered: false));

    /// <summary>
    /// The pad's colour under the pointer. Kept separate from <see cref="NormalBackground"/> so
    /// the two can be attached to the theme's own normal and pointer-over states, which is what
    /// makes hovering a *playing* pad brighten rather than go dark.
    /// </summary>
    public IBrush HoverBackground => new SolidColorBrush(StateColor(hovered: true));

    /// <summary>
    /// Black or white, whichever stays legible on the colour behind it. The colours are the user's
    /// to choose, so a fixed foreground would eventually be unreadable on one of them.
    /// </summary>
    public IBrush NormalForeground => ContrastingText(StateColor(hovered: false));

    public IBrush HoverForeground => ContrastingText(StateColor(hovered: true));

    partial void OnIsPausedChanged(bool value) => OnPropertyChanged(nameof(PauseGlyph));

    partial void OnIsLitChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTransport));
        RaiseColorsChanged();
        UpdateProgressTimer();
    }

    private void RaiseColorsChanged()
    {
        OnPropertyChanged(nameof(NormalBackground));
        OnPropertyChanged(nameof(HoverBackground));
        OnPropertyChanged(nameof(NormalForeground));
        OnPropertyChanged(nameof(HoverForeground));
    }

    private Color StateColor(bool hovered)
    {
        var palette = PadPaletteResolver.Resolve(Config.Colors, _page.PadColors);
        var chosen = (IsLit, hovered) switch
        {
            (true, true) => palette.ActiveHover,
            (true, false) => palette.Active,
            (false, true) => palette.InactiveHover,
            (false, false) => palette.Inactive,
        };

        return Parse(chosen, IsLit ? PadPaletteResolver.DefaultActive : PadPaletteResolver.DefaultInactive);
    }

    private static IBrush ContrastingText(Color background) =>
        new SolidColorBrush(IsLight(background) ? Colors.Black : Colors.White);

    /// <summary>Parses a stored colour, falling back rather than throwing — a hand-edited setup
    /// file shouldn't be able to take the whole grid down.</summary>
    private static Color Parse(string value, string fallback) =>
        Color.TryParse(value, out var color) ? color : Color.Parse(fallback);

    /// <summary>Rec. 601 luma, the usual quick test for whether dark text will read on a colour.</summary>
    private static bool IsLight(Color color) =>
        ((color.R * 299) + (color.G * 587) + (color.B * 114)) / 1000 > 140;

    /// <summary>Sends playback back to the beginning without stopping it.</summary>
    [RelayCommand]
    private void Restart() => _audioEngine.Restart(Config.Id);

    /// <summary>Holds playback where it is, or lets it carry on.</summary>
    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        _audioEngine.SetPaused(Config.Id, IsPaused);
    }

    /// <summary>
    /// Runs the readout only while there is something to read out, so idle pads cost nothing.
    /// </summary>
    private void UpdateProgressTimer()
    {
        if (IsLit)
        {
            RefreshProgress();
            _progressTimer.Start();
            return;
        }

        _progressTimer.Stop();
        IsPaused = false;
        TimeDisplay = string.Empty;
    }

    private void RefreshProgress()
    {
        if (_audioEngine.GetProgress(Config.Id) is not { } progress)
        {
            TimeDisplay = string.Empty;
            return;
        }

        IsPaused = progress.IsPaused;

        // A looping clip is going round again, so what matters is where it is in the bar. A
        // one-shot is going to stop, so what matters is how long is left before it does.
        TimeDisplay = Config.Mode == PlaybackMode.Loop
            ? $"{Format(progress.Elapsed)} / {Format(progress.Total)}"
            : Format(progress.Remaining);
    }

    private static string Format(TimeSpan value) => $"{(int)value.TotalMinutes}:{value.Seconds:00}";

    /// <summary>Re-reads display state from <see cref="Config"/> after it's been edited and saved.</summary>
    public void RefreshFromConfig()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(VolumePercent));
        OnPropertyChanged(nameof(ShowTransport));
        RaiseColorsChanged();
        IconBitmap = LoadIconBitmap(Config.IconPath);
    }

    /// <summary>
    /// Stops listening to the audio engine, for a pad discarded by a grid resize. The engine
    /// outlives individual pads, so without this its event would keep the dropped view model
    /// alive and firing.
    /// </summary>
    public void Detach()
    {
        _progressTimer.Stop();
        _audioEngine.PlaybackStateChanged -= OnPlaybackStateChanged;
    }

    /// <summary>
    /// What pressing the pad does: open its settings while the board is being arranged, otherwise
    /// start or stop its clip. One press, one meaning, decided by the mode rather than by how long
    /// the press lasted.
    /// </summary>
    [RelayCommand]
    private void Activate()
    {
        if (_editMode.IsEditing)
        {
            ConfigRequested?.Invoke(this);
            return;
        }

        _audioEngine.Play(Config);
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        if (e.PadId != Config.Id)
        {
            return;
        }

        // EndReached-driven stops arrive from a background thread; marshal onto the UI thread.
        Dispatcher.UIThread.Post(() => IsLit = e.IsPlaying);
    }

    private static Bitmap? LoadIconBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
