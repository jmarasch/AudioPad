namespace AudioPad.Core.Models;

/// <summary>The saved configuration for one grid button: which clip it plays, how, and how it's labeled.</summary>
public sealed class PadConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public int Row { get; set; }

    public int Column { get; set; }

    public string Label { get; set; } = string.Empty;

    public string? IconPath { get; set; }

    public string? AudioFilePath { get; set; }

    public PlaybackMode Mode { get; set; } = PlaybackMode.Latch;

    /// <summary>Output volume for this pad, from 0.0 (silent) to 1.0 (full).</summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>True once an audio file has been assigned. Pads without one render as empty/unconfigured.</summary>
    public bool HasAudio => !string.IsNullOrWhiteSpace(AudioFilePath);
}
