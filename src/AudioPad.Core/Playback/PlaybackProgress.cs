namespace AudioPad.Core.Playback;

/// <summary>
/// Where a playing pad has got to, for the time readout on its face. Polled by the UI rather than
/// pushed as an event: playback position changes continuously, and the display only needs it a few
/// times a second while a pad is actually lit.
/// </summary>
/// <param name="Elapsed">How far into the clip playback currently is.</param>
/// <param name="Total">The clip's full length, or zero while libVLC hasn't determined it yet.</param>
/// <param name="IsPaused">Whether playback is holding its position rather than advancing.</param>
public readonly record struct PlaybackProgress(TimeSpan Elapsed, TimeSpan Total, bool IsPaused)
{
    /// <summary>How much of the clip is left — what a one-shot pad counts down to zero.</summary>
    public TimeSpan Remaining => Total > Elapsed ? Total - Elapsed : TimeSpan.Zero;
}
