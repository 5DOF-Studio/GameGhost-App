using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// Voice delivery decision for brain results heading to voice agent.
/// </summary>
public enum DeliveryDecision
{
    /// <summary>Deliver to voice agent now.</summary>
    Deliver,

    /// <summary>Suppress — do not deliver to voice.</summary>
    Suppress,

    /// <summary>Queue as reminder for next exchange opening (12C).</summary>
    QueueReminder,
}

/// <summary>
/// Centralizes all voice delivery decisions. BrainEventRouter delegates to this
/// instead of checking exchange/barge-in/reminder state directly.
/// Absorbs exchange-related deps that would otherwise bloat BrainEventRouter constructor (W6/W7).
/// </summary>
public interface IVoiceDeliveryGate
{
    /// <summary>
    /// Decide whether a brain result should be delivered to the voice agent.
    /// Uses default category (FreeCommentary) when result type is unknown.
    /// </summary>
    DeliveryDecision ShouldDeliver(BrainResultPriority priority);

    /// <summary>
    /// Decide delivery with barge-in category consideration (12C full matrix).
    /// Maps BrainResultType to BargeInCategory for barge-in policy checks.
    /// </summary>
    DeliveryDecision ShouldDeliver(BrainResultPriority priority, BrainResultType resultType);
}
