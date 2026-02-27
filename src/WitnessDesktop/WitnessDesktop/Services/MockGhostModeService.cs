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

    public void ShowCard(FabCardVariant variant, string? title, string? text, string? imagePath)
    {
        Console.WriteLine($"[MockGhostModeService] ShowCard: variant={variant}, title={title} (no-op)");
    }

    public void DismissCard()
    {
        Console.WriteLine("[MockGhostModeService] DismissCard (no-op)");
    }

}
