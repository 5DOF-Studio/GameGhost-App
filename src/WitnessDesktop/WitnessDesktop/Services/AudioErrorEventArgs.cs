namespace WitnessDesktop.Services;

/// <summary>
/// Event arguments for <see cref="IAudioService.ErrorOccurred"/>.
/// </summary>
public sealed class AudioErrorEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new audio error event.
    /// </summary>
    /// <param name="message">Human-readable error description.</param>
    /// <param name="exception">Optional underlying exception.</param>
    /// <param name="isFatal">
    /// If true, the audio system is in an unrecoverable state and must be restarted.
    /// If false, the error is transient and operation may continue.
    /// </param>
    public AudioErrorEventArgs(string message, Exception? exception = null, bool isFatal = false)
    {
        Message = message;
        Exception = exception;
        IsFatal = isFatal;
    }

    /// <summary>
    /// Human-readable error description.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Optional underlying exception that caused the error.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// If true, the audio system is in an unrecoverable state (e.g., permission denied, device unavailable).
    /// If false, the error is transient and operation may continue.
    /// </summary>
    public bool IsFatal { get; }
}
