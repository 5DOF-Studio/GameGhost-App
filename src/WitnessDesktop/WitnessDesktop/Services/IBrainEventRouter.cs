using System.Threading.Channels;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Services;

public interface IBrainEventRouter
{
    void OnScreenCapture(string screenshotRef, TimeSpan gameTime, string method);

    void OnBrainHint(BrainHint hint);

    void OnImageAnalysis(string analysisText);

    void OnDirectMessage(ChatMessage userMsg, ChatMessage brainResponse);

    /// <summary>
    /// Route a single assistant message to the timeline without re-emitting the user prompt.
    /// </summary>
    void OnAssistantMessage(ChatMessage assistantMsg);

    void OnProactiveAlert(BrainHint hint, string commentary);

    void OnGeneralChat(string text);

    void OnError(string message);

    /// <summary>
    /// Route a single user message to the timeline (in-game text chat).
    /// Adds a DirectMessage event with Role=User.
    /// </summary>
    void OnUserMessage(ChatMessage userMsg);

    /// <summary>Fired when the top strip text should update (brain analysis, alerts, captures).</summary>
    event Action<string>? TopStripUpdated;

    /// <summary>
    /// Fired when a brain chat reply is received via the Channel pipeline (ToolResult type).
    /// Subscribers (MainViewModel) use this to add replies to ChatMessages.
    /// </summary>
    event Action<string>? BrainChatReplyReceived;

    /// <summary>
    /// Fired when a tool call is routed to the timeline. Subscribers use this
    /// to surface tool-use status in the ghost card pipeline.
    /// </summary>
    event Action<ToolCallInfo>? ToolCallReceived;

    /// <summary>
    /// Fired when the emission queue pushes a drip-fed analysis event (Danger, Assessment, SageAdvice).
    /// Subscribers (MainViewModel) forward to ghost mode overlay.
    /// </summary>
    event Action<TimelineEvent>? AnalysisEventEmitted;

    /// <summary>
    /// Fired immediately when structured analysis is expanded into an ordered batch.
    /// Ghost mode uses this to rotate through the full batch without waiting for the
    /// main timeline drip cadence.
    /// </summary>
    event Action<IReadOnlyList<TimelineEvent>>? AnalysisBatchQueued;

    /// <summary>
    /// Fired when a terminal brain failure has been routed and the session should disconnect.
    /// </summary>
    event Action<BrainResult>? TerminalBrainErrorReceived;

    /// <summary>
    /// Start consuming BrainResult from the brain service's channel.
    /// Call once after IBrainService is initialized. Runs until cancelled.
    /// </summary>
    void StartConsuming(ChannelReader<BrainResult> reader, CancellationToken ct);

    /// <summary>
    /// Stop the channel consumer loop. Safe to call multiple times.
    /// </summary>
    void StopConsuming();
}
