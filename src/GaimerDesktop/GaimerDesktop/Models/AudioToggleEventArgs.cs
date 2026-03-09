namespace GaimerDesktop.Models;

/// <summary>
/// Event args for ghost mode audio toggle changes.
/// Raised when a user taps a toggle in the native audio control card.
/// </summary>
public class AudioToggleEventArgs : EventArgs
{
    /// <summary>Toggle index: 0=MIC, 1=AUTO (Commentary), 2=AI-MIC.</summary>
    public int ToggleIndex { get; }

    /// <summary>New toggle value.</summary>
    public bool NewValue { get; }

    public AudioToggleEventArgs(int toggleIndex, bool newValue)
    {
        ToggleIndex = toggleIndex;
        NewValue = newValue;
    }
}
