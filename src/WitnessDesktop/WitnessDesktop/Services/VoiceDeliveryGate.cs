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

    public VoiceDeliveryGate(
        IExchangeManager exchangeManager,
        IBargeInPolicyService? bargeInPolicy = null,
        IUserSpeechDetector? userSpeechDetector = null,
        IAgentSpeechTracker? agentSpeechTracker = null)
    {
        _exchangeManager = exchangeManager ?? throw new ArgumentNullException(nameof(exchangeManager));
        _bargeInPolicy = bargeInPolicy;
        _userSpeechDetector = userSpeechDetector;
        _agentSpeechTracker = agentSpeechTracker;
    }

    public DeliveryDecision ShouldDeliver(BrainResultPriority priority)
        => ShouldDeliver(priority, BrainResultType.ImageAnalysis); // Default: treated as FreeCommentary

    public DeliveryDecision ShouldDeliver(BrainResultPriority priority, BrainResultType resultType)
    {
        // 1. Silent → always suppress
        if (priority == BrainResultPriority.Silent)
            return DeliveryDecision.Suppress;

        // 2. Exchange active → deliver
        if (_exchangeManager.IsExchangeActive)
            return DeliveryDecision.Deliver;

        // 3. Interrupt → deliver (safety override)
        if (priority == BrainResultPriority.Interrupt)
            return DeliveryDecision.Deliver;

        // --- Exchange inactive, non-interrupt, non-silent ---

        // No barge-in service → suppress (pre-12C backward compat)
        if (_bargeInPolicy == null)
            return DeliveryDecision.Suppress;

        // 4. Barge-in disabled → queue reminder
        if (!_bargeInPolicy.IsBargeInEnabled)
            return DeliveryDecision.QueueReminder;

        // Map result type to barge-in category
        var category = MapToCategory(resultType);
        if (category == null)
            return DeliveryDecision.Suppress; // Error type — no barge-in category

        // 5. Category not allowed → queue reminder
        if (!_bargeInPolicy.IsCategoryAllowed(category.Value))
            return DeliveryDecision.QueueReminder;

        // 6. D-AI-4: User speaking → queue reminder (NEVER barge in while user speaking)
        if (_userSpeechDetector?.IsUserSpeaking == true)
            return DeliveryDecision.QueueReminder;

        // 6b. D-AI-4: Agent speaking → queue reminder (NEVER barge in while agent speaking)
        if (_agentSpeechTracker?.IsSpeaking == true)
            return DeliveryDecision.QueueReminder;

        // 7. All checks passed → deliver (barge-in!)
        return DeliveryDecision.Deliver;
    }

    private static BargeInCategory? MapToCategory(BrainResultType type) => type switch
    {
        BrainResultType.ProactiveAlert => BargeInCategory.CallOut,
        BrainResultType.ToolResult => BargeInCategory.ToolExecution,
        BrainResultType.ImageAnalysis => BargeInCategory.FreeCommentary,
        _ => null, // Error type → no category
    };
}
