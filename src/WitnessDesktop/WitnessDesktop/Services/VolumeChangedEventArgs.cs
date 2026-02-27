namespace WitnessDesktop.Services;

/// <summary>
/// Event arguments for <see cref="IAudioService.VolumeChanged"/>.
/// Contains the current RMS volume levels for input (microphone) and output (playback).
/// </summary>
public sealed class VolumeChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance with the specified volume levels.
    /// </summary>
    /// <param name="inputVolume">Input (microphone) RMS volume in [0..1].</param>
    /// <param name="outputVolume">Output (playback) RMS volume in [0..1].</param>
    public VolumeChangedEventArgs(float inputVolume, float outputVolume)
    {
        InputVolume = inputVolume;
        OutputVolume = outputVolume;
    }

    /// <summary>
    /// Input (microphone) RMS volume in the range [0..1].
    /// </summary>
    public float InputVolume { get; }

    /// <summary>
    /// Output (playback) RMS volume in the range [0..1].
    /// </summary>
    public float OutputVolume { get; }
}
