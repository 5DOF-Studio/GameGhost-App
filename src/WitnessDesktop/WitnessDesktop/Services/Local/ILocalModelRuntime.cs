namespace WitnessDesktop.Services.Local;

/// <summary>
/// Abstraction for a local model runtime (e.g. Ollama, direct service).
/// Intentionally narrow — only health checks in this plan.
/// Inference methods will be added in subsequent plans.
/// </summary>
public interface ILocalModelRuntime
{
    /// <summary>Check runtime and model availability.</summary>
    Task<LocalRuntimeHealth> GetHealthAsync(CancellationToken ct = default);
}
