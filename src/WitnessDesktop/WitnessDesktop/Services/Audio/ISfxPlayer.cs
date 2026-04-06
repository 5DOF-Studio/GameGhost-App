namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Dedicated sound effects player. Independent of the voice playback pipeline.
/// Used for wake confirmation pings, barge-in warnings, and other UI sounds.
/// Plays at a configurable volume without interrupting agent speech.
/// </summary>
public interface ISfxPlayer
{
    /// <summary>Play a named sound effect from Resources/Raw at the given volume (0.0-1.0).</summary>
    Task PlayAsync(string fileName, float volume = 0.25f);
}
