using AudioPad.Core.Models;
using AudioPad.Core.Playback;

namespace AudioPad.UI;

/// <summary>
/// No-op <see cref="IAudioEngine"/> used only as a design-time/previewer fallback, so
/// <c>AudioPad.UI</c> never needs a direct dependency on the real libVLC-backed engine in
/// <c>AudioPad.Audio</c>. Platform heads (Desktop/Android) always supply the real engine.
/// </summary>
public sealed class NullAudioEngine : IAudioEngine
{
    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged
    {
        add { }
        remove { }
    }

    public void Play(PadConfig pad)
    {
    }

    public void Stop(Guid padId)
    {
    }

    public void StopAll()
    {
    }

    public bool IsPlaying(Guid padId) => false;

    public void SetVolume(Guid padId, float volume)
    {
    }

    public void Dispose()
    {
    }
}
