using AudioPad.Core.Models;
using AudioPad.Core.Playback;
using LibVLCSharp.Shared;

namespace AudioPad.Audio;

/// <summary>
/// <see cref="IAudioEngine"/> backed by libVLC. Owns one shared <see cref="LibVLC"/> instance and
/// one <see cref="MediaPlayer"/> per currently-playing pad, so multiple clips can play at once.
/// </summary>
public sealed class LibVlcAudioEngine : IAudioEngine
{
    private readonly LibVLC _libVlc;
    private readonly Dictionary<Guid, MediaPlayer> _players = new();
    private readonly object _lock = new();

    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

    public LibVlcAudioEngine()
    {
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC();
    }

    public void Play(PadConfig pad)
    {
        lock (_lock)
        {
            if (_players.ContainsKey(pad.Id))
            {
                // Pressing an already-playing pad always interrupts it, in both Latch and Loop.
                StopUnlocked(pad.Id);
                return;
            }

            if (pad.HasAudio)
            {
                StartUnlocked(pad);
            }
        }
    }

    public void Stop(Guid padId)
    {
        lock (_lock)
        {
            StopUnlocked(padId);
        }
    }

    public void StopAll()
    {
        lock (_lock)
        {
            foreach (var padId in _players.Keys.ToArray())
            {
                StopUnlocked(padId);
            }
        }
    }

    public bool IsPlaying(Guid padId)
    {
        lock (_lock)
        {
            return _players.ContainsKey(padId);
        }
    }

    public void SetVolume(Guid padId, float volume)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(padId, out var player))
            {
                player.Volume = ToLibVlcVolume(volume);
            }
        }
    }

    public void Dispose()
    {
        StopAll();
        _libVlc.Dispose();
    }

    private void StartUnlocked(PadConfig pad)
    {
        var media = new Media(_libVlc, new Uri(pad.AudioFilePath!));
        var player = new MediaPlayer(media)
        {
            Volume = ToLibVlcVolume(pad.Volume),
        };
        media.Dispose(); // MediaPlayer retains its own reference; this copy is no longer needed.

        player.EndReached += (_, _) => OnEndReached(pad);

        _players[pad.Id] = player;
        player.Play();
        RaisePlaybackStateChanged(pad.Id, isPlaying: true);
    }

    private void StopUnlocked(Guid padId)
    {
        if (!_players.Remove(padId, out var player))
        {
            return;
        }

        player.Stop();
        player.Dispose();
        RaisePlaybackStateChanged(padId, isPlaying: false);
    }

    /// <summary>
    /// Runs off libVLC's own callback thread: calling <see cref="MediaPlayer.Stop"/> synchronously
    /// from within an EndReached callback deadlocks, since Stop blocks until VLC's internal
    /// threads join.
    /// </summary>
    private void OnEndReached(PadConfig pad)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            lock (_lock)
            {
                if (!_players.TryGetValue(pad.Id, out var player))
                {
                    // Already stopped explicitly (e.g. pressed again right as it finished).
                    return;
                }

                if (pad.Mode == PlaybackMode.Loop)
                {
                    player.Stop();
                    player.Play();
                    return;
                }

                StopUnlocked(pad.Id);
            }
        });
    }

    private void RaisePlaybackStateChanged(Guid padId, bool isPlaying) =>
        PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs { PadId = padId, IsPlaying = isPlaying });

    private static int ToLibVlcVolume(float volume) => (int)Math.Clamp(volume * 100f, 0f, 100f);
}
