# Plan 04: Timeline Feed Manager

**Phase:** 02-chat-brain  
**Status:** ✅ Complete  
**Estimated Effort:** 4-6 hours  
**Actual Effort:** ~30 min (combined with Plan 03)  
**Dependencies:** 01-core-models, 03-brain-event-router  
**Provides:** Checkpoint-based timeline feed service

---

## Objective

Implement `TimelineFeed` — the service that manages the hierarchical timeline structure, creating checkpoints on captures and stacking events by type within checkpoints.

---

## Tasks

### 1. Timeline Feed Interface

**File:** `src/GaimerDesktop/GaimerDesktop/Services/ITimelineFeed.cs`

```csharp
public interface ITimelineFeed
{
    /// <summary>
    /// All checkpoints in the timeline (for UI binding).
    /// </summary>
    ObservableCollection<TimelineCheckpoint> Checkpoints { get; }
    
    /// <summary>
    /// The current (most recent) checkpoint.
    /// </summary>
    TimelineCheckpoint? CurrentCheckpoint { get; }
    
    /// <summary>
    /// Creates a new checkpoint for an in-game screen capture.
    /// </summary>
    TimelineCheckpoint NewCapture(string screenshotRef, TimeSpan gameTime, string method);
    
    /// <summary>
    /// Creates a new checkpoint for an out-game conversation turn.
    /// </summary>
    TimelineCheckpoint NewConversationCheckpoint();
    
    /// <summary>
    /// Adds an event to the current checkpoint. Stacks on existing line if same type.
    /// </summary>
    void AddEvent(TimelineEvent evt);
    
    /// <summary>
    /// Clears all checkpoints (e.g., on session end).
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Fired when a new checkpoint is created (for auto-scroll).
    /// </summary>
    event EventHandler<TimelineCheckpoint>? CheckpointCreated;
}
```

### 2. Timeline Feed Implementation

**File:** `src/GaimerDesktop/GaimerDesktop/Services/TimelineFeed.cs`

```csharp
public class TimelineFeed : ITimelineFeed
{
    private readonly ISessionManager _sessionManager;
    
    public ObservableCollection<TimelineCheckpoint> Checkpoints { get; } = new();
    
    public TimelineCheckpoint? CurrentCheckpoint => Checkpoints.LastOrDefault();
    
    public event EventHandler<TimelineCheckpoint>? CheckpointCreated;
    
    public TimelineFeed(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }
    
    public TimelineCheckpoint NewCapture(string screenshotRef, TimeSpan gameTime, string method)
    {
        var checkpoint = new TimelineCheckpoint
        {
            Context = SessionState.InGame,
            GameTimeIn = gameTime,
            ScreenshotRef = screenshotRef,
            CaptureMethod = method,
        };
        
        Checkpoints.Add(checkpoint);
        CheckpointCreated?.Invoke(this, checkpoint);
        
        return checkpoint;
    }
    
    public TimelineCheckpoint NewConversationCheckpoint()
    {
        var checkpoint = new TimelineCheckpoint
        {
            Context = SessionState.OutGame,
        };
        
        Checkpoints.Add(checkpoint);
        CheckpointCreated?.Invoke(this, checkpoint);
        
        return checkpoint;
    }
    
    public void AddEvent(TimelineEvent evt)
    {
        var checkpoint = EnsureCurrentCheckpoint();
        
        var existingLine = checkpoint.EventLines
            .FirstOrDefault(l => l.OutputType == evt.Type);
        
        if (existingLine != null)
        {
            existingLine.Events.Add(evt);
        }
        else
        {
            checkpoint.EventLines.Add(new EventLine
            {
                OutputType = evt.Type,
                Events = new ObservableCollection<TimelineEvent> { evt },
            });
        }
    }
    
    public void Clear()
    {
        Checkpoints.Clear();
    }
    
    #region Helpers
    
    private TimelineCheckpoint EnsureCurrentCheckpoint()
    {
        if (Checkpoints.Count == 0)
        {
            // Auto-create checkpoint based on current session state
            if (_sessionManager.CurrentState == SessionState.InGame)
            {
                return NewCapture("auto", TimeSpan.Zero, "auto");
            }
            else
            {
                return NewConversationCheckpoint();
            }
        }
        
        return Checkpoints.Last();
    }
    
    #endregion
}
```

### 3. Checkpoint Header Display Logic

Add computed properties to `TimelineCheckpoint` for UI:

**Update:** `src/GaimerDesktop/GaimerDesktop/Models/Timeline/TimelineCheckpoint.cs`

```csharp
public class TimelineCheckpoint
{
    // ... existing properties ...
    
    /// <summary>
    /// Display header for the checkpoint (e.g., "📷 Capture #7 — 3:12 in" or "◯ — 8:42 PM")
    /// </summary>
    public string DisplayHeader
    {
        get
        {
            if (Context == SessionState.InGame && GameTimeIn.HasValue)
            {
                return $"📷 Capture — {GameTimeIn.Value:m\\:ss} in";
            }
            else
            {
                return $"◯ — {Timestamp:h:mm tt}";
            }
        }
    }
    
    /// <summary>
    /// Context badge text (e.g., "[in-game]" or "[out-game]")
    /// </summary>
    public string ContextBadge => Context == SessionState.InGame ? "[in-game]" : "[out-game]";
}
```

### 4. Event Stacking Logic

Events of the same type within a checkpoint stack horizontally. Add helper for UI:

**Update:** `src/GaimerDesktop/GaimerDesktop/Models/Timeline/EventLine.cs`

```csharp
public class EventLine
{
    public EventOutputType OutputType { get; set; }
    public ObservableCollection<TimelineEvent> Events { get; set; } = new();
    
    /// <summary>
    /// Icon for the line (from first event or default for type).
    /// </summary>
    public string LineIcon => Events.FirstOrDefault()?.Icon ?? GetDefaultIcon(OutputType);
    
    /// <summary>
    /// Whether this line has multiple stacked events.
    /// </summary>
    public bool HasMultipleEvents => Events.Count > 1;
    
    private static string GetDefaultIcon(EventOutputType type) => type switch
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
}
```

### 5. DI Registration

**File:** `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs`

```csharp
builder.Services.AddSingleton<ITimelineFeed, TimelineFeed>();
```

---

## Rendering Rules

From the design spec:

| Rule | Behavior |
|------|----------|
| New capture | New checkpoint header (section divider) |
| Different output type | New line within checkpoint |
| Same output type, same checkpoint | Stack horizontally on same line |
| User direct message | Message icon + full bubble within checkpoint |
| New capture arrives | Auto-scroll to latest checkpoint |

---

## Verification

- [ ] `NewCapture` creates InGame checkpoint with game time
- [ ] `NewConversationCheckpoint` creates OutGame checkpoint
- [ ] `AddEvent` stacks events of same type on existing line
- [ ] `AddEvent` creates new line for different event type
- [ ] `CheckpointCreated` event fires on new checkpoint
- [ ] `EnsureCurrentCheckpoint` auto-creates if none exists
- [ ] `DisplayHeader` returns correct format for InGame vs OutGame
- [ ] `Clear` removes all checkpoints

---

## Timeline Structure Example

```
Checkpoints [ObservableCollection<TimelineCheckpoint>]
├── Checkpoint #1 (InGame, 3:12)
│   └── EventLines [ObservableCollection<EventLine>]
│       ├── EventLine (ImageAnalysis)
│       │   └── Events: [🖼️ "White pawn to e4..."]
│       └── EventLine (Opportunity)
│           └── Events: [⚡ "Fork available"]
│
├── Checkpoint #2 (InGame, 3:18)
│   └── EventLines
│       ├── EventLine (Danger)
│       │   └── Events: [⚠️ "Blunder! Queen exposed"]
│       └── EventLine (DirectMessage)
│           └── Events: [💬 User: "what happened?", 💬 Brain: "Your opponent..."]
│
└── Checkpoint #3 (OutGame, 8:42 PM)
    └── EventLines
        └── EventLine (GeneralChat)
            └── Events: [💭 "How did my session go?", 💭 "You won 2 of 3..."]
```

---

## Files to Create

```
src/GaimerDesktop/GaimerDesktop/Services/ITimelineFeed.cs
src/GaimerDesktop/GaimerDesktop/Services/TimelineFeed.cs
```

## Files to Modify

```
src/GaimerDesktop/GaimerDesktop/Models/Timeline/TimelineCheckpoint.cs
src/GaimerDesktop/GaimerDesktop/Models/Timeline/EventLine.cs
src/GaimerDesktop/GaimerDesktop/MauiProgram.cs
```
