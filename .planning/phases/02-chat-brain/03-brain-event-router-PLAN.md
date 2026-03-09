# Plan 03: Brain Event Router

**Phase:** 02-chat-brain  
**Status:** ✅ Complete  
**Estimated Effort:** 4-6 hours  
**Actual Effort:** ~30 min (combined with Plan 04)  
**Dependencies:** 01-core-models  
**Provides:** Central event routing hub for Brain → Timeline/Voice/TopStrip

---

## Objective

Implement `BrainEventRouter` — the central hub that sits between the Brain analysis pipeline and all output consumers (Timeline Feed, Voice Agent, Top Strip status bar).

---

## Architecture

```
                    ┌───────────────────────────────────┐
                    │     Brain Analysis Pipeline       │
                    │  (screen capture → analysis →     │
                    │   interest detection → hints)     │
                    └──────────────┬────────────────────┘
                                   │
                          BrainEventRouter
                          ┌────────┼────────┐
                          │        │        │
                          ▼        ▼        ▼
                     Timeline   Voice    Top Strip
                       Feed    Agent    (status bar)
```

---

## Tasks

### 1. Router Interface

**File:** `src/GaimerDesktop/GaimerDesktop/Services/IBrainEventRouter.cs`

```csharp
public interface IBrainEventRouter
{
    /// <summary>
    /// New screen capture — creates new checkpoint in timeline.
    /// </summary>
    void OnScreenCapture(string screenshotRef, TimeSpan gameTime, string method);
    
    /// <summary>
    /// Brain detected an interesting pattern — routes to all consumers.
    /// </summary>
    void OnBrainHint(BrainHint hint);
    
    /// <summary>
    /// Image analysis complete — adds to current checkpoint.
    /// </summary>
    void OnImageAnalysis(string analysisText);
    
    /// <summary>
    /// User sent message and received response — adds bubble to timeline.
    /// </summary>
    void OnDirectMessage(ChatMessage userMsg, ChatMessage brainResponse);
    
    /// <summary>
    /// Proactive brain alert — distinct from assistant response.
    /// </summary>
    void OnProactiveAlert(BrainHint hint, string commentary);
}
```

### 2. Brain Hint Model

**File:** `src/GaimerDesktop/GaimerDesktop/Models/BrainHint.cs`

```csharp
public class BrainHint
{
    public string Signal { get; init; } = "none";     // danger, opportunity, blunder, brilliant, opening
    public string Urgency { get; init; } = "low";     // high, medium, low
    public string Summary { get; init; } = string.Empty;
    public string? SuggestedMove { get; init; }       // UCI notation or action
    public int Evaluation { get; init; }              // Centipawns
    public int? EvalDelta { get; init; }              // Change from previous
}
```

### 3. Router Implementation

**File:** `src/GaimerDesktop/GaimerDesktop/Services/BrainEventRouter.cs`

```csharp
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
            Icon = GetIconForType(outputType),
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
            _ = _voiceAgent.SendContextualUpdateAsync(FormatForVoice(hint));
        }
    }
    
    public void OnImageAnalysis(string analysisText)
    {
        _timeline.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.ImageAnalysis,
            Icon = "🖼️",
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
            Icon = "💬",
            Summary = Truncate(userMsg.Content, 60),
            FullContent = userMsg.Content,
            Role = MessageRole.User,
            LinkedMessage = userMsg,
        });
        
        _timeline.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Icon = "💬",
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
            Icon = GetIconForType(outputType),
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
            _ = _voiceAgent.SendContextualUpdateAsync(FormatForVoice(hint));
        }
    }
    
    #region Helpers
    
    private static EventOutputType MapSignalToOutputType(string signal) => signal switch
    {
        "danger" => EventOutputType.Danger,
        "blunder" => EventOutputType.Danger,
        "opportunity" => EventOutputType.Opportunity,
        "brilliant" => EventOutputType.Opportunity,
        "opening" => EventOutputType.Tactic,
        _ => EventOutputType.PositionEval,
    };
    
    private static string GetIconForType(EventOutputType type) => type switch
    {
        EventOutputType.Tactic => "⚔️",
        EventOutputType.PositionEval => "📊",
        EventOutputType.BestMove => "🎯",
        EventOutputType.Danger => "⚠️",
        EventOutputType.Opportunity => "⚡",
        EventOutputType.GameStateChange => "🏁",
        EventOutputType.DirectMessage => "💬",
        EventOutputType.ImageAnalysis => "🖼️",
        EventOutputType.AnalyticsResult => "📈",
        EventOutputType.HistoryRecall => "📋",
        EventOutputType.GeneralChat => "💭",
        _ => "📝",
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
```

### 4. IConversationProvider Extension

Add contextual update support to the provider interface:

**File:** `src/GaimerDesktop/GaimerDesktop/Services/Conversation/IConversationProvider.cs`

```csharp
// Add to interface:
Task SendContextualUpdateAsync(string contextText, CancellationToken ct = default);
```

### 5. DI Registration

**File:** `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs`

```csharp
builder.Services.AddSingleton<IBrainEventRouter>(sp =>
{
    var timeline = sp.GetRequiredService<ITimelineFeed>();
    var provider = sp.GetService<IConversationProvider>();
    // TopStrip callback would be wired via MainViewModel
    return new BrainEventRouter(timeline, provider, null);
});
```

---

## Integration Points

### MainViewModel Wiring

```csharp
// On frame captured:
_brainEventRouter.OnScreenCapture(
    screenshotRef: frameId,
    gameTime: DateTime.UtcNow - _gameStartTime,
    method: "auto"
);

// On brain hint detected:
_brainEventRouter.OnBrainHint(hint);

// On user sends message and receives response:
_brainEventRouter.OnDirectMessage(userMessage, aiResponse);
```

---

## Verification

- [ ] `OnScreenCapture` creates new checkpoint in timeline
- [ ] `OnBrainHint` adds event to current checkpoint
- [ ] `OnBrainHint` sends to voice agent when connected
- [ ] `OnBrainHint` updates top strip
- [ ] `OnDirectMessage` adds both user and response events
- [ ] `OnProactiveAlert` adds event with `MessageRole.Proactive`
- [ ] Icon mapping returns correct emoji for each `EventOutputType`

---

## Files to Create

```
src/GaimerDesktop/GaimerDesktop/Services/IBrainEventRouter.cs
src/GaimerDesktop/GaimerDesktop/Services/BrainEventRouter.cs
src/GaimerDesktop/GaimerDesktop/Models/BrainHint.cs
```

## Files to Modify

```
src/GaimerDesktop/GaimerDesktop/Services/Conversation/IConversationProvider.cs
src/GaimerDesktop/GaimerDesktop/MauiProgram.cs
src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs
```
