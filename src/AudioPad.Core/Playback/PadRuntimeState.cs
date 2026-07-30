namespace AudioPad.Core.Playback;

/// <summary>Tracks the live playback status of a single pad, independent of its saved configuration.</summary>
public sealed class PadRuntimeState
{
    public required Guid PadId { get; init; }

    public bool IsPlaying { get; set; }
}
