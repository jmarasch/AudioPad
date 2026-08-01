using AudioPad.Core.Models;

namespace AudioPad.Core.Playback;

/// <summary>
/// Plays and stops audio clips for pads, independent of the concrete audio backend (see
/// AudioPad.Audio for the libVLC-based implementation). Multiple pads may play concurrently.
/// </summary>
public interface IAudioEngine : IDisposable
{
    /// <summary>Raised whenever a pad's playing state changes, so the UI can update its "lit" indicator.</summary>
    event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

    /// <summary>
    /// Starts playback for the given pad using its configured file, mode, and volume. If the pad
    /// is already playing in Latch mode, this instead stops it (press-to-interrupt behavior).
    /// </summary>
    void Play(PadConfig pad);

    /// <summary>Stops playback for the given pad, if it is currently playing.</summary>
    void Stop(Guid padId);

    /// <summary>Stops every currently playing pad.</summary>
    void StopAll();

    /// <summary>Returns whether the given pad is currently playing.</summary>
    bool IsPlaying(Guid padId);

    /// <summary>Updates the output volume (0.0-1.0) for a pad, live if it is currently playing.</summary>
    void SetVolume(Guid padId, float volume);

    /// <summary>Sends a playing pad back to the start of its clip without interrupting playback.</summary>
    void Restart(Guid padId);

    /// <summary>
    /// Holds a playing pad at its current position, or lets it continue. Distinct from
    /// <see cref="Stop"/>, which discards the position entirely.
    /// </summary>
    void SetPaused(Guid padId, bool paused);

    /// <summary>
    /// How far through its clip a pad currently is, or null if it isn't playing at all.
    /// </summary>
    PlaybackProgress? GetProgress(Guid padId);
}
