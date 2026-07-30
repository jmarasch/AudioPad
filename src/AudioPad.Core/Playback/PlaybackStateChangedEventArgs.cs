namespace AudioPad.Core.Playback;

/// <summary>Raised by an <see cref="IAudioEngine"/> when a pad starts or stops playing.</summary>
public sealed class PlaybackStateChangedEventArgs : EventArgs
{
    public required Guid PadId { get; init; }

    public required bool IsPlaying { get; init; }
}
