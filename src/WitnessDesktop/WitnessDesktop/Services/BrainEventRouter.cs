using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services.Conversation;

namespace WitnessDesktop.Services;

public class BrainEventRouter : IBrainEventRouter
{
    private readonly ITimelineFeed _timeline;
    private readonly IConversationProvider? _voiceAgent;
    private readonly Action<string>? _topStrip;
    
    public BrainEventRouter(
        ITimelineFeed timeline,
        IConversationProvider? voiceAgent = null,
        Action<string>? topStrip = null)
    {
        _timeline = timeline;
        _voiceAgent = voiceAgent;
        _topStrip = topStrip;
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
