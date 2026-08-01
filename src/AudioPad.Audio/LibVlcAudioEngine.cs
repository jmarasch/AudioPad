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
    /// <summary>
    /// When to re-apply a pad's volume after its clip starts. See
    /// <see cref="ReapplyVolumeAfterOutputSettles"/> — the first is soon enough to keep the
    /// correction inaudible, the later ones cover slower devices.
    /// </summary>
    private static readonly int[] VolumeSettleDelays = [60, 220, 500];

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

    public void Restart(Guid padId)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(padId, out var player))
            {
                // Seeking rather than stopping and starting again: it keeps the audio output alive,
                // so the pad's volume survives — the same teardown that made looping lose its level.
                player.Time = 0;
            }
        }
    }

    public void SetPaused(Guid padId, bool paused)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(padId, out var player))
            {
                // SetPause rather than Pause(), which toggles — the caller states the state it wants.
                player.SetPause(paused);
            }
        }
    }

    public PlaybackProgress? GetProgress(Guid padId)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue(padId, out var player))
            {
                return null;
            }

            // Both are -1 until libVLC has read enough of the file to know.
            var elapsed = TimeSpan.FromMilliseconds(Math.Max(player.Time, 0));
            var total = TimeSpan.FromMilliseconds(Math.Max(player.Length, 0));
            return new PlaybackProgress(elapsed, total, !player.IsPlaying);
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

        // libVLC's volume applies to the audio output, which is torn down and rebuilt around a
        // stop — so a volume set while stopped can be dropped. Re-applying once playback has
        // actually begun is what makes the setting stick on every pass of a loop, not just the
        // first. Deliberately not taking the engine lock: this arrives on a libVLC thread, and
        // the loop restart that triggers it already holds that lock.
        player.Playing += (_, _) => ReapplyVolumeAfterOutputSettles(pad);

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

    /// <summary>
    /// Re-applies a pad's level a moment after its clip starts, then twice more.
    ///
    /// libVLC tears down and rebuilds the audio output every time a clip stops and starts — for a
    /// looping pad, on every pass. The host audio stack then restores a remembered level onto each
    /// newly created output, and it remembers per *application*, so all of AudioPad's clips share
    /// one entry: whatever played most recently. A quiet looping pad would come back at the volume
    /// of some loud pad pressed in the meantime.
    ///
    /// Setting the level as the clip starts is too early to survive that restore; setting it
    /// shortly afterwards holds, and updates what gets remembered so later passes start correct.
    /// The delays are spaced rather than single because how quickly the output is established
    /// varies by device.
    /// </summary>
    private void ReapplyVolumeAfterOutputSettles(PadConfig pad)
    {
        _ = Task.Run(async () =>
        {
            foreach (var delay in VolumeSettleDelays)
            {
                await Task.Delay(delay);

                lock (_lock)
                {
                    // The pad may have been stopped, or restarted onto a different player, since.
                    if (!_players.TryGetValue(pad.Id, out var current))
                    {
                        return;
                    }

                    current.Volume = ToLibVlcVolume(pad.Volume);
                }
            }
        });
    }

    private static int ToLibVlcVolume(float volume) => (int)Math.Clamp(volume * 100f, 0f, 100f);
}
