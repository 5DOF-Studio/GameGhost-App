namespace WitnessDesktop.Models.Exchange;

/// <summary>
/// Immutable snapshot of a single exchange instance.
/// ExchangeManager produces new instances on each state transition (with-expression).
/// </summary>
public sealed record ExchangeSession
{
    public Guid ExchangeId { get; init; } = Guid.NewGuid();
    public ExchangeState State { get; init; } = ExchangeState.Dormant;
    public DateTime OpenedAtUtc { get; init; }
    public DateTime LastActivityUtc { get; init; }
    public string? AgentName { get; init; }
}
