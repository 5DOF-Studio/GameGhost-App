namespace WitnessDesktop.Models.Exchange;

/// <summary>
/// A deferred brain result queued for surfacing at the next exchange opening.
/// </summary>
public sealed class ReminderItem
{
    public required string Content { get; init; }
    public required BargeInCategory Category { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public Guid? ExchangeId { get; init; }
    /// <summary>Whether this reminder has been superseded by a fresher result in the same category.</summary>
    public bool IsSuperseded { get; set; }
    /// <summary>Check if this reminder is stale (older than maxAge).</summary>
    public bool IsStale(DateTime nowUtc, TimeSpan maxAge) => (nowUtc - CreatedAtUtc) >= maxAge;
}
