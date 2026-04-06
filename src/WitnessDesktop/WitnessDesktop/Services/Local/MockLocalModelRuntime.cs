namespace WitnessDesktop.Services.Local;

/// <summary>
/// DI-safe local runtime stub used when the real local runtime (e.g. Ollama) is not yet implemented.
/// Reports unavailable by default — configure via constructor to simulate healthy local runtime in tests.
/// </summary>
public sealed class MockLocalModelRuntime : ILocalModelRuntime
{
    private readonly LocalRuntimeHealth _health;

    /// <summary>
    /// Creates a MockLocalModelRuntime. Default: runtime unavailable.
    /// </summary>
    public MockLocalModelRuntime(bool available = false, bool brain = false, bool voice = false, string? failureReason = null)
    {
        _health = new LocalRuntimeHealth
        {
            RuntimeAvailable = available,
            BrainAvailable = brain,
            VoiceAvailable = voice,
            RuntimeName = "mock",
            ModelId = available ? "mock-model" : null,
            FailureReason = available ? null : (failureReason ?? "Local runtime not yet implemented")
        };
    }

    public Task<LocalRuntimeHealth> GetHealthAsync(CancellationToken ct = default)
        => Task.FromResult(_health);
}
