namespace WitnessDesktop.Models.Exchange;

/// <summary>
/// Priority request from voice to brain. Carries the user's question,
/// the likely brain capability needed, and exchange context (spec Section 14).
/// </summary>
public sealed class BrainRequest
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public Guid? ExchangeId { get; init; }
    public required string UserQuestion { get; init; }
    public string? LikelyCapability { get; init; }
    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;
    public bool HasDeferralBeenSpoken { get; set; }
    public DateTime? ExpiresAtUtc { get; init; }
}
