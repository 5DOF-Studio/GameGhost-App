using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// No-op implementation of <see cref="IGhostModeService"/> for non-macOS platforms
/// and testing. All methods are no-ops with Console.WriteLine for debug tracing.
/// </summary>
public class MockGhostModeService : IGhostModeService
{
    public bool IsGhostModeActive => false;
    public bool IsSupported => false;

#pragma warning disable CS0067 // Events declared but never fired (intentional for mock)
    public event EventHandler? FabTapped;
    public event EventHandler? CardDismissed;
    public event EventHandler? GearTapped;
    public event EventHandler<AudioToggleEventArgs>? AudioToggleChanged;
#pragma warning restore CS0067

    public Task EnterGhostModeAsync()
    {
        Console.WriteLine("[MockGhostModeService] EnterGhostModeAsync (no-op)");
        return Task.CompletedTask;
    }

    public Task ExitGhostModeAsync()
    {
        Console.WriteLine("[MockGhostModeService] ExitGhostModeAsync (no-op)");
        return Task.CompletedTask;
    }

    public void SetAgentImage(string imagePath)
    {
        Console.WriteLine($"[MockGhostModeService] SetAgentImage: {imagePath} (no-op)");
    }

    public void SetFabState(bool active, bool connected)
    {
        Console.WriteLine($"[MockGhostModeService] SetFabState: active={active}, connected={connected} (no-op)");
    }

    public void ShowCard(FabCardVariant variant, string? title, string? text, string? imagePath,
                         bool isAlert = false, bool isVoiceDelivered = false)
    {
        Console.WriteLine($"[MockGhostModeService] ShowCard: variant={variant}, title={title}, alert={isAlert}, voiceDelivered={isVoiceDelivered} (no-op)");
    }

    public void DismissCard()
    {
        Console.WriteLine("[MockGhostModeService] DismissCard (no-op)");
    }

    public void SetPosition(double x, double y) { }

    public void SetSize(double width, double height) { }

    public void SetAudioState(bool voiceChatActive, bool voiceCommandActive,
                              bool gameAudioActive, bool audioInActive)
    {
        Console.WriteLine($"[MockGhostModeService] SetAudioState: voiceChat={voiceChatActive}, voiceCommand={voiceCommandActive}, gameAudio={gameAudioActive}, audioIn={audioInActive} (no-op)");
    }

    public void SetVadLevel(float level)
    {
        // no-op for non-macOS
    }

    public void SetExchangeState(int state) { }
}
