using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Services;

/// <summary>
/// Exchange-aware voice delivery gate with full barge-in decision matrix (12C).
/// Decision paths:
///   1. Silent → Suppress
///   2. Exchange active → Deliver
///   3. Exchange inactive + Interrupt → Deliver (safety override)
///   4. Exchange inactive + barge-in disabled → QueueReminder
///   5. Exchange inactive + barge-in enabled + category NOT allowed → QueueReminder
///   6. Exchange inactive + barge-in enabled + allowed + user speaking → QueueReminder (D-AI-4)
///   6b. Exchange inactive + barge-in enabled + allowed + agent speaking → QueueReminder (D-AI-4)
///   7. Exchange inactive + barge-in enabled + allowed + user+agent silent → Deliver (barge-in)
/// When barge-in deps are null (pre-12C), falls back to Suppress for inactive+non-interrupt.
/// </summary>
public sealed class VoiceDeliveryGate : IVoiceDeliveryGate
{
    private readonly IExchangeManager _exchangeManager;
    private readonly IBargeInPolicyService? _bargeInPolicy;
    private readonly IUserSpeechDetector? _userSpeechDetector;
    private readonly IAgentSpeechTracker? _agentSpeechTracker;
    private readonly ISessionTraceService? _sessionTrace;

    public VoiceDeliveryGate(
        IExchangeManager exchangeManager,
        IBargeInPolicyService? bargeInPolicy = null,
        IUserSpeechDetector? userSpeechDetector = null,
        IAgentSpeechTracker? agentSpeechTracker = null,
        ISessionTraceService? sessionTrace = null)
    {
        _exchangeManager = exchangeManager ?? throw new ArgumentNullException(nameof(exchangeManager));
        _bargeInPolicy = bargeInPolicy;
        _userSpeechDetector = userSpeechDetector;
        _agentSpeechTracker = agentSpeechTracker;
        _sessionTrace = sessionTrace;
    }

    public DeliveryDecision ShouldDeliver(BrainResultPriority priority)
        => ShouldDeliver(priority, BrainResultType.ImageAnalysis); // Default: treated as FreeCommentary

    public DeliveryDecision ShouldDeliver(BrainResultPriority priority, BrainResultType resultType)
    {
        DeliveryDecision decision;
        string reason;

        // 1. Silent → always suppress
        if (priority == BrainResultPriority.Silent)
        {
            decision = DeliveryDecision.Suppress;
            reason = "silent_priority";
        }
        // 2. Exchange active → deliver
        else if (_exchangeManager.IsExchangeActive)
        {
            decision = DeliveryDecision.Deliver;
            reason = "exchange_active";
        }
        // 3. Interrupt → deliver (safety override)
        else if (priority == BrainResultPriority.Interrupt)
        {
            decision = DeliveryDecision.Deliver;
            reason = "interrupt_override";
        }
        // --- Exchange inactive, non-interrupt, non-silent ---
        // No barge-in service → suppress (pre-12C backward compat)
        else if (_bargeInPolicy == null)
        {
            decision = DeliveryDecision.Suppress;
            reason = "no_bargein_service";
        }
        // 4. Barge-in disabled → queue reminder
        else if (!_bargeInPolicy.IsBargeInEnabled)
        {
            decision = DeliveryDecision.QueueReminder;
            reason = "bargein_disabled";
        }
        else
        {
            // Map result type to barge-in category
            var category = MapToCategory(resultType);
            if (category == null)
            {
                decision = DeliveryDecision.Suppress;
                reason = "no_bargein_category";
            }
            // 5. Category not allowed → queue reminder
            else if (!_bargeInPolicy.IsCategoryAllowed(category.Value))
            {
                decision = DeliveryDecision.QueueReminder;
                reason = "category_not_allowed";
            }
            // 6. D-AI-4: User speaking → queue reminder (NEVER barge in while user speaking)
            else if (_userSpeechDetector?.IsUserSpeaking == true)
            {
                decision = DeliveryDecision.QueueReminder;
                reason = "user_speaking";
            }
            // 6b. D-AI-4: Agent speaking → queue reminder (NEVER barge in while agent speaking)
            else if (_agentSpeechTracker?.IsSpeaking == true)
            {
                decision = DeliveryDecision.QueueReminder;
                reason = "agent_speaking";
            }
            // 7. All checks passed → deliver (barge-in!)
            else
            {
                decision = DeliveryDecision.Deliver;
                reason = "bargein_allowed";
            }
        }

        _sessionTrace?.TrackEvent("voice.delivery.decision", new Dictionary<string, string>
        {
            ["result_type"] = resultType.ToString(),
            ["decision"] = decision.ToString(),
            ["reason"] = reason
        });

        return decision;
    }

    private static BargeInCategory? MapToCategory(BrainResultType type) => type switch
    {
        BrainResultType.ProactiveAlert => BargeInCategory.CallOut,
        BrainResultType.ToolResult => BargeInCategory.ToolExecution,
        BrainResultType.ImageAnalysis => BargeInCategory.FreeCommentary,
        _ => null, // Error type → no category
    };
}
