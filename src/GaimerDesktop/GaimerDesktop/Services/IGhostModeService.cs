using GaimerDesktop.Models;

namespace GaimerDesktop.Services;

/// <summary>
/// Cross-platform interface for ghost mode overlay operations.
/// Ghost mode hides the MAUI window and shows a native floating panel
/// with a FAB (Floating Action Button) and event cards over gameplay.
/// </summary>
public interface IGhostModeService
{
    /// <summary>Whether ghost mode overlay is currently active.</summary>
    bool IsGhostModeActive { get; }

    /// <summary>Whether the native ghost mode overlay is supported on this platform.</summary>
    bool IsSupported { get; }

    /// <summary>Enter ghost mode: hide MAUI window, show native panel.</summary>
    Task EnterGhostModeAsync();

    /// <summary>Exit ghost mode: hide native panel, show MAUI window.</summary>
    Task ExitGhostModeAsync();

    /// <summary>Update the agent image shown on the FAB.</summary>
    void SetAgentImage(string imagePath);

    /// <summary>Update FAB state (active/connected).</summary>
    void SetFabState(bool active, bool connected);

    /// <summary>Show an event card in ghost mode.</summary>
    void ShowCard(FabCardVariant variant, string? title, string? text, string? imagePath);

    /// <summary>Dismiss the current event card.</summary>
    void DismissCard();

    /// <summary>Position the ghost panel at the given screen coordinates (AppKit origin).</summary>
    void SetPosition(double x, double y);

    /// <summary>Sync audio toggle states from C# to the native audio card.</summary>
    void SetAudioState(bool micActive, bool commentaryActive, bool aiMicActive);

    /// <summary>Fired when user taps the FAB in ghost mode.</summary>
    event EventHandler? FabTapped;

    /// <summary>Fired when event card is dismissed (tap or timeout).</summary>
    event EventHandler? CardDismissed;

    /// <summary>Fired when user taps the gear icon in ghost mode.</summary>
    event EventHandler? GearTapped;

    /// <summary>Fired when an audio toggle changes in the native audio card.</summary>
    event EventHandler<AudioToggleEventArgs>? AudioToggleChanged;
}
