namespace AudioPad.Core.Models;

/// <summary>How a pad's playback behaves when pressed.</summary>
public enum PlaybackMode
{
    /// <summary>Plays through once. Pressing the pad again while it's playing interrupts and stops it.</summary>
    Latch,

    /// <summary>Loops continuously until the pad is pressed again to stop it.</summary>
    Loop,
}
