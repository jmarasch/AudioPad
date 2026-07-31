using AudioPad.Core.Models;
using AudioPad.Core.Playback;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPad.UI.ViewModels;

/// <summary>Wraps a <see cref="PadConfig"/> with the UI-facing playing/"lit" state for one grid button.</summary>
public sealed partial class PadViewModel : ViewModelBase
{
    private readonly IAudioEngine _audioEngine;

    public PadConfig Config { get; }

    [ObservableProperty]
    private bool _isLit;

    [ObservableProperty]
    private Bitmap? _iconBitmap;

    /// <summary>Raised when the pad is double-tapped, asking the owner to open its config dialog.</summary>
    public event Action<PadViewModel>? ConfigRequested;

    public PadViewModel(PadConfig config, IAudioEngine audioEngine)
    {
        Config = config;
        _audioEngine = audioEngine;
        _audioEngine.PlaybackStateChanged += OnPlaybackStateChanged;
        IconBitmap = LoadIconBitmap(config.IconPath);
    }

    public string Label => Config.Label;

    /// <summary>Re-reads display state from <see cref="Config"/> after it's been edited and saved.</summary>
    public void RefreshFromConfig()
    {
        OnPropertyChanged(nameof(Label));
        IconBitmap = LoadIconBitmap(Config.IconPath);
    }

    [RelayCommand]
    private void Toggle() => _audioEngine.Play(Config);

    [RelayCommand]
    private void OpenConfig() => ConfigRequested?.Invoke(this);

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
