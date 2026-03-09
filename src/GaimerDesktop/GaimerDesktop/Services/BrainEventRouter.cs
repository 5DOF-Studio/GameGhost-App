using System.Threading.Channels;
using GaimerDesktop.Models;
using GaimerDesktop.Models.Timeline;
using GaimerDesktop.Services.Conversation;

namespace GaimerDesktop.Services;

public class BrainEventRouter : IBrainEventRouter
{
    private readonly ITimelineFeed _timeline;
    private readonly IConversationProvider? _voiceAgent;
    private readonly Action<string>? _topStrip;
    private readonly IBrainContextService? _brainContext;

    private CancellationTokenSource? _consumeCts;
    private Task? _consumeTask;

    public BrainEventRouter(
        ITimelineFeed timeline,
        IConversationProvider? voiceAgent = null,
        Action<string>? topStrip = null,
        IBrainContextService? brainContext = null)
    {
        _timeline = timeline;
        _voiceAgent = voiceAgent;
        _topStrip = topStrip;
        _brainContext = brainContext;
    }
    
    public void OnScreenCapture(string screenshotRef, TimeSpan gameTime, string method)
    {
        _timeline.NewCapture(screenshotRef, gameTime, method);
        _topStrip?.Invoke($"Analyzing capture at {gameTime:m\\:ss}...");
    }
    
    public void OnBrainHint(BrainHint hint)
    {
        var outputType = MapSignalToOutputType(hint.Signal);

        var evt = new TimelineEvent
        {
            Type = outputType,
            Icon = EventIconMap.GetIcon(outputType),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(outputType),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(outputType),
            Summary = hint.Summary,
            Brain = new BrainMetadata
            {
                Signal = hint.Signal,
                Urgency = hint.Urgency,
                Evaluation = hint.Evaluation,
                EvalDelta = hint.EvalDelta,
                SuggestedAction = hint.SuggestedMove,
            },
        };
        
        _timeline.AddEvent(evt);
        _topStrip?.Invoke(hint.Summary);
        
        if (_voiceAgent?.IsConnected == true)
        {
            _ = _voiceAgent.SendContextualUpdateAsync(FormatForVoice(hint))
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[BrainEventRouter] Voice update failed: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    public void OnImageAnalysis(string analysisText)
    {
        _timeline.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.ImageAnalysis,
            Icon = EventIconMap.GetIcon(EventOutputType.ImageAnalysis),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.ImageAnalysis),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.ImageAnalysis),
            Summary = Truncate(analysisText, 80),
            FullContent = analysisText,
        });
        _topStrip?.Invoke(analysisText);
    }
    
    public void OnDirectMessage(ChatMessage userMsg, ChatMessage brainResponse)
    {
        _timeline.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Icon = EventIconMap.GetIcon(EventOutputType.DirectMessage),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.DirectMessage),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.DirectMessage),
            Summary = Truncate(userMsg.Content, 60),
            FullContent = userMsg.Content,
            Role = MessageRole.User,
            LinkedMessage = userMsg,
        });

        _timeline.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Icon = EventIconMap.GetIcon(EventOutputType.DirectMessage),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.DirectMessage),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.DirectMessage),
            Summary = Truncate(brainResponse.Content, 60),
            FullContent = brainResponse.Content,
            Role = MessageRole.Assistant,
            LinkedMessage = brainResponse,
        });
    }
    
    public void OnProactiveAlert(BrainHint hint, string commentary)
    {
        var outputType = MapSignalToOutputType(hint.Signal);

        _timeline.AddEvent(new TimelineEvent
        {
            Type = outputType,
            Icon = EventIconMap.GetIcon(outputType),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(outputType),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(outputType),
            Summary = commentary,
            FullContent = commentary,
            Role = MessageRole.Proactive,
            Brain = new BrainMetadata
            {
                Signal = hint.Signal,
                Urgency = hint.Urgency,
                Evaluation = hint.Evaluation,
                EvalDelta = hint.EvalDelta,
                SuggestedAction = hint.SuggestedMove,
            },
        });
        
        _topStrip?.Invoke(commentary);
        
        if (_voiceAgent?.IsConnected == true && hint.Urgency == "high")
        {
            _ = _voiceAgent.SendContextualUpdateAsync(FormatForVoice(hint))
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[BrainEventRouter] Voice update failed: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);
        }
    }
    
    public void OnGeneralChat(string text)
    {
        _timeline.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.GeneralChat,
            Icon = EventIconMap.GetIcon(EventOutputType.GeneralChat),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.GeneralChat),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.GeneralChat),
            Summary = Truncate(text, 80),
            FullContent = text,
        });
        _topStrip?.Invoke(text);
    }

    public void OnError(string message)
    {
        _timeline.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.SystemError,
            Icon = EventIconMap.GetIcon(EventOutputType.SystemError),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.SystemError),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.SystemError),
            Summary = Truncate(message, 80),
            FullContent = message,
            Role = MessageRole.System,
        });
    }

    #region Channel Consumer

    public void StartConsuming(ChannelReader<BrainResult> reader, CancellationToken ct)
    {
        StopConsuming(); // Idempotent — stop any prior loop
        _consumeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _consumeTask = Task.Run(() => ConsumeBrainResultsAsync(reader, _consumeCts.Token));
    }

    public void StopConsuming()
    {
        _consumeCts?.Cancel();
        _consumeCts?.Dispose();
        _consumeCts = null;
        // Don't await _consumeTask — it's fire-and-forget with internal error handling
        _consumeTask = null;
    }

    private async Task ConsumeBrainResultsAsync(ChannelReader<BrainResult> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var result in reader.ReadAllAsync(ct))
            {
                try
                {
                    RouteBrainResult(result);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[BrainEventRouter] Error routing result {result.Type}: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { /* Normal shutdown */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[BrainEventRouter] Consumer loop crashed: {ex.Message}");
        }
    }

    private void RouteBrainResult(BrainResult result)
    {
        switch (result.Type)
        {
            case BrainResultType.ImageAnalysis:
                if (result.AnalysisText != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                        OnImageAnalysis(result.AnalysisText));
                }
                break;

            case BrainResultType.ProactiveAlert:
                if (result.Hint != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                        OnProactiveAlert(result.Hint, result.AnalysisText ?? result.Hint.Summary));
                }
                break;

            case BrainResultType.ToolResult:
                if (result.AnalysisText != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                        OnGeneralChat(result.AnalysisText));
                }
                break;

            case BrainResultType.Error:
                System.Diagnostics.Debug.WriteLine(
                    $"[BrainEventRouter] Brain error: {result.AnalysisText}");
                if (result.AnalysisText != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                        OnError(result.AnalysisText));
                }
                break;
        }

        // Ingest as L1 event for context pipeline (BRAIN_VOICE_PIPELINE_RULES Section 4)
        if (_brainContext != null && result.AnalysisText != null)
        {
            var brainEvent = new BrainEvent
            {
                TimestampUtc = result.CreatedAt.UtcDateTime,
                Type = result.Type switch
                {
                    BrainResultType.ImageAnalysis => BrainEventType.VisionObservation,
                    BrainResultType.ProactiveAlert => BrainEventType.GameplayState,
                    BrainResultType.ToolResult => BrainEventType.GameplayState,
                    BrainResultType.Error => BrainEventType.SystemSignal,
                    _ => BrainEventType.SystemSignal
                },
                Category = result.Type switch
                {
                    BrainResultType.ImageAnalysis => "vision",
                    BrainResultType.ProactiveAlert => result.Hint?.Signal ?? "alert",
                    BrainResultType.ToolResult => "tool",
                    BrainResultType.Error => "error",
                    _ => "unknown"
                },
                Text = result.AnalysisText,
                Confidence = result.Type == BrainResultType.Error ? 0.0 : 0.8,
                ValidFor = result.Type == BrainResultType.ImageAnalysis
                    ? TimeSpan.FromSeconds(30)
                    : TimeSpan.FromMinutes(2)
            };
            _ = _brainContext.IngestEventAsync(brainEvent)
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[BrainEventRouter] L1 ingest failed: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);
        }

        // Voice forwarding based on priority
        if (_voiceAgent?.IsConnected == true && result.VoiceNarration != null)
        {
            switch (result.Priority)
            {
                case BrainResultPriority.Interrupt:
                    _ = _voiceAgent.SendContextualUpdateAsync($"[URGENT] {result.VoiceNarration}")
                        .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                            $"[BrainEventRouter] Voice interrupt failed: {t.Exception?.GetBaseException().Message}"),
                            TaskContinuationOptions.OnlyOnFaulted);
                    break;
                case BrainResultPriority.WhenIdle:
                    _ = _voiceAgent.SendContextualUpdateAsync(result.VoiceNarration)
                        .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                            $"[BrainEventRouter] Voice update failed: {t.Exception?.GetBaseException().Message}"),
                            TaskContinuationOptions.OnlyOnFaulted);
                    break;
                // Silent: no voice forwarding
            }
        }
    }

    #endregion

    #region Helpers
    
    private static EventOutputType MapSignalToOutputType(string signal) => signal switch
    {
        "danger" => EventOutputType.Danger,
        "blunder" => EventOutputType.Danger,
        "opportunity" => EventOutputType.Opportunity,
        "brilliant" => EventOutputType.Opportunity,
        "sage" => EventOutputType.SageAdvice,
        "assessment" => EventOutputType.Assessment,
        "detection" => EventOutputType.Detection,
        _ => EventOutputType.SageAdvice,
    };
    
    private static string FormatForVoice(BrainHint hint)
    {
        var parts = new List<string>
        {
            $"[BRAIN SIGNAL] {hint.Signal}",
            hint.Urgency,
            $"Eval: {hint.Evaluation}cp",
        };
        
        if (hint.EvalDelta.HasValue)
            parts.Add($"Delta: {hint.EvalDelta}cp");
        if (hint.SuggestedMove != null)
            parts.Add($"Suggested: {hint.SuggestedMove}");
        
        return string.Join(" | ", parts);
    }
    
    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..(maxLen - 1)] + "…";
    
    #endregion
}
