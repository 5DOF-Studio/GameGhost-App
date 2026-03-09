# Plan 01: Core Models

**Phase:** 02-chat-brain  
**Status:** ✅ Complete  
**Estimated Effort:** 5-7 hours  
**Actual Effort:** ~1 hour  
**Dependencies:** None  
**Provides:** Foundation data structures for all other plans

---

## Objective

Implement the core data models from the Chat Brain Design spec:
- Session context and state
- Message roles and intents
- Timeline hierarchy (Checkpoint → EventLine → Event)
- Brain metadata and tool call info
- **Generic vs agent-specific event types**
- **Agent availability gating**

---

## Tasks

### 0. Agent Model Updates (Feature Gating)

**File:** `src/GaimerDesktop/GaimerDesktop/Models/Agent.cs`

Add `IsAvailable` flag and agent type enum:

```csharp
public enum AgentType
{
    General,    // Derek - RPG
    Chess,      // Leroy - Chess
    Fps         // Wasp - FPS
}

public class Agent
{
    // ... existing properties ...
    
    /// <summary>
    /// Agent type for event type resolution.
    /// </summary>
    public required AgentType Type { get; init; }
    
    /// <summary>
    /// Whether this agent is available for selection.
    /// Use to gate unreleased/incomplete agents.
    /// </summary>
    public bool IsAvailable { get; init; } = true;
}

public static class Agents
{
    public static Agent General { get; } = new()
    {
        // ... existing ...
        Type = AgentType.General,
        IsAvailable = false,  // Gated until RPG events/tools designed
    };
    
    public static Agent Chess { get; } = new()
    {
        // ... existing ...
        Type = AgentType.Chess,
        IsAvailable = true,   // Only available agent initially
    };
    
    public static Agent Wasp { get; } = new()
    {
        // ... existing ...
        Type = AgentType.Fps,
        IsAvailable = false,  // Gated until FPS events/tools designed
    };
    
    /// <summary>
    /// All agents (including unavailable, for admin/debug).
    /// </summary>
    public static IReadOnlyList<Agent> All { get; } = [General, Chess, Wasp];
    
    /// <summary>
    /// Only agents available for user selection.
    /// </summary>
    public static IReadOnlyList<Agent> Available => All.Where(a => a.IsAvailable).ToList();
}
```

**Update AgentSelectionPage** to use `Agents.Available` instead of `Agents.All`.

---

### 1. Session Models

**File:** `src/GaimerDesktop/GaimerDesktop/Models/SessionContext.cs`

```csharp
public enum SessionState
{
    OutGame,   // No active game. Brain visual pipeline idle.
    InGame     // Game connected. Screen capture + analysis active.
}

public class SessionContext
{
    public SessionState State { get; set; } = SessionState.OutGame;
    public string? GameId { get; set; }
    public string? GameType { get; set; }        // "chess", "fps", "moba", etc.
    public string? ConnectorName { get; set; }   // "Chess.com", "Steam", etc.
    public DateTime? GameStartedAt { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserTier { get; set; } = "Adventurer";
}
```

### 2. Message Models (Update Existing)

**File:** `src/GaimerDesktop/GaimerDesktop/Models/ChatMessage.cs`

Update existing `ChatMessage` to align with design:

```csharp
public enum MessageRole
{
    User,       // Player typed something
    Assistant,  // Brain responding to user
    System,     // Internal context (not displayed, but in LLM history)
    Proactive   // Brain-initiated (blunder alert, opportunity, etc.)
}

public enum MessageIntent
{
    GeneralChat,      // Casual talk
    GamingHistory,    // "How did I do last session?"
    Analytics,        // "What's my win rate?"
    LiveGameInfo,     // "What should I do?" (IN-GAME ONLY)
    ImageAnalysis,    // Brain analyzed a screen capture
    BrainAlert,       // Proactive: blunder, opportunity, danger
    ToolResult        // Internal: tool call result injected into context
}

public class ChatMessage : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MessageRole Role { get; set; }
    public MessageIntent Intent { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public BrainMetadata? Brain { get; set; }
    public ToolCallInfo? ToolCall { get; set; }
    public DeliveryState DeliveryState { get; set; } = DeliveryState.None;
}
```

### 3. Brain Metadata

**File:** `src/GaimerDesktop/GaimerDesktop/Models/BrainMetadata.cs`

```csharp
public class BrainMetadata
{
    public string? Signal { get; set; }           // "danger", "opportunity", "blunder", etc.
    public string? Urgency { get; set; }          // "high", "medium", "low"
    public int? Evaluation { get; set; }          // Centipawns (game-specific)
    public int? EvalDelta { get; set; }
    public string? SuggestedAction { get; set; }  // "e2e4" or game-specific
    public string? ScreenshotRef { get; set; }
}
```

### 4. Tool Call Info

**File:** `src/GaimerDesktop/GaimerDesktop/Models/ToolCallInfo.cs`

```csharp
public class ToolCallInfo
{
    public string ToolName { get; set; } = string.Empty;
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public int DurationMs { get; set; }
    public bool Success { get; set; }
}
```

### 5. Timeline Models

**File:** `src/GaimerDesktop/GaimerDesktop/Models/Timeline/TimelineCheckpoint.cs`

```csharp
public class TimelineCheckpoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public SessionState Context { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan? GameTimeIn { get; set; }        // In-game only
    public string? ScreenshotRef { get; set; }       // In-game only
    public string? CaptureMethod { get; set; }       // "auto", "algorithmic", "manual"
    public ObservableCollection<EventLine> EventLines { get; set; } = new();
}
```

**File:** `src/GaimerDesktop/GaimerDesktop/Models/Timeline/EventLine.cs`

```csharp
public class EventLine
{
    public EventOutputType OutputType { get; set; }
    public ObservableCollection<TimelineEvent> Events { get; set; } = new();
}
```

**File:** `src/GaimerDesktop/GaimerDesktop/Models/Timeline/TimelineEvent.cs`

```csharp
public class TimelineEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public EventOutputType Type { get; set; }
    public string? Icon { get; set; }                // Resolved from Type, overridable
    public string Summary { get; set; } = string.Empty;
    public string? FullContent { get; set; }         // Expanded view
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public BrainMetadata? Brain { get; set; }
    public MessageRole? Role { get; set; }           // For direct messages
    public ChatMessage? LinkedMessage { get; set; }  // Full message if user-initiated
}
```

**File:** `src/GaimerDesktop/GaimerDesktop/Models/Timeline/EventOutputType.cs`

```csharp
/// <summary>
/// Generic event types usable by all agents.
/// Agent-specific events use values >= AgentSpecific.
/// </summary>
public enum EventOutputType
{
    // === GENERIC (All Agents) ===
    
    // In-game outputs
    Danger,           // High-risk alert (blunder, low HP, ambush)
    Opportunity,      // Advantageous action available
    SageAdvice,       // Tactical guidance, best moves, build advice
    Assessment,       // Generic state evaluation
    GameStateChange,  // Phase/state transition (new game, round end, etc.)
    Detection,        // User-primed vision detection (call out if seen)

    // Both contexts
    DirectMessage,    // User typed a message + Brain response
    ImageAnalysis,    // Screen analysis text output

    // Out-game outputs
    AnalyticsResult,  // Win rate, performance stats
    HistoryRecall,    // Past session reference
    GeneralChat,      // Casual conversation
    
    // === AGENT-SPECIFIC MARKER ===
    AgentSpecific = 100
}
```

**File:** `src/GaimerDesktop/GaimerDesktop/Models/Timeline/ChessEventType.cs`

```csharp
/// <summary>
/// Chess-specific event types (Leroy agent).
/// </summary>
public enum ChessEventType
{
    PositionEval = EventOutputType.AgentSpecific + 1,  // Centipawn evaluation from engine
    Tactic           // Best move, fork, pin, skewer, opening patterns
}
```

**File:** `src/GaimerDesktop/GaimerDesktop/Models/Timeline/FpsEventType.cs`

```csharp
/// <summary>
/// FPS-specific event types (Wasp agent) — placeholder for future.
/// </summary>
public enum FpsEventType
{
    ThreatDetected = EventOutputType.AgentSpecific + 1,
    CoverAdvice,
    LoadoutTip,
    ObjectiveUpdate,
    RotationCall
}
```

**File:** `src/GaimerDesktop/GaimerDesktop/Models/Timeline/RpgEventType.cs`

```csharp
/// <summary>
/// RPG-specific event types (Derek agent).
/// </summary>
public enum RpgEventType
{
    QuestHint = EventOutputType.AgentSpecific + 1,  // Quest/objective guidance
    Lore,             // World/story context
    ItemAlert         // Notable loot spotted
}
```

### 6. Event Icon Mapping

**File:** `src/GaimerDesktop/GaimerDesktop/Models/Timeline/EventIconMap.cs`

```csharp
/// <summary>
/// Maps event types to icon assets. Supports generic + agent-specific icons.
/// NO ICON REUSE — each event type has its own unique asset.
/// </summary>
public static class EventIconMap
{
    // Generic event icons (11 types)
    private static readonly Dictionary<EventOutputType, string> GenericIcons = new()
    {
        [EventOutputType.Danger] = "alert.png",
        [EventOutputType.Opportunity] = "opportunity.png",
        [EventOutputType.SageAdvice] = "sage.png",
        [EventOutputType.Assessment] = "assessment.png",
        [EventOutputType.GameStateChange] = "new-game.png",
        [EventOutputType.Detection] = "detection.png",
        [EventOutputType.DirectMessage] = "chat.png",
        [EventOutputType.ImageAnalysis] = "video-reel.png",
        [EventOutputType.AnalyticsResult] = "performance.png",
        [EventOutputType.HistoryRecall] = "history-clock.png",
        [EventOutputType.GeneralChat] = "call-out.png",
    };
    
    // Chess-specific icons (Leroy) — 2 types
    private static readonly Dictionary<ChessEventType, string> ChessIcons = new()
    {
        [ChessEventType.PositionEval] = "chessboard-eval.png",
        [ChessEventType.Tactic] = "chess-move.png",
    };
    
    // RPG-specific icons (Derek) — 3 types
    private static readonly Dictionary<RpgEventType, string> RpgIcons = new()
    {
        [RpgEventType.QuestHint] = "quest.png",
        [RpgEventType.Lore] = "lore.png",
        [RpgEventType.ItemAlert] = "item-alert.png",
    };
    
    public static string GetIcon(EventOutputType type) =>
        GenericIcons.TryGetValue(type, out var icon) ? icon : "info.png";
    
    public static string GetIcon(ChessEventType type) =>
        ChessIcons.TryGetValue(type, out var icon) ? icon : "info.png";
    
    public static string GetIcon(RpgEventType type) =>
        RpgIcons.TryGetValue(type, out var icon) ? icon : "info.png";
    
    public static string GetIcon(int eventCode, AgentType agent)
    {
        if (eventCode < (int)EventOutputType.AgentSpecific)
            return GetIcon((EventOutputType)eventCode);
        
        return agent switch
        {
            AgentType.Chess => GetIcon((ChessEventType)eventCode),
            AgentType.General => GetIcon((RpgEventType)eventCode),
            // Future: AgentType.Fps => GetIcon((FpsEventType)eventCode),
            _ => "info.png"
        };
    }
}
```

---

## Verification

- [x] All models compile without errors
- [x] `SessionState` enum has `OutGame` and `InGame` values
- [x] `MessageRole` includes `Proactive` distinct from `Assistant`
- [x] `TimelineCheckpoint` contains `ObservableCollection<EventLine>`
- [x] `EventLine` groups events by `EventOutputType`
- [x] `TimelineEvent` can link to `ChatMessage` for direct messages
- [x] Existing `ChatMessage` usage sites updated for new properties (marked `[Obsolete]`)
- [x] **`Agent.IsAvailable` flag added to model**
- [x] **`Agents.Available` returns only gated agents (initially just Chess)**
- [x] **AgentSelectionViewModel uses `Agents.Available`**
- [x] **`EventOutputType` has 11 generic types + `AgentSpecific` marker**
- [x] **`ChessEventType` has 2 types: PositionEval, Tactic**
- [x] **`RpgEventType` has 3 types: QuestHint, Lore, ItemAlert**
- [x] **`EventIconMap` resolves correct icons with no reuse**
- [x] **All 16 icon assets present in `assets/` folder**

### Implementation Notes (2026-02-24)

- Old `ChatMessage.Type` and `ChatMessage.Text` properties marked `[Obsolete]` with migration guidance
- New `MessageRole` and `MessageIntent` enums added alongside old `ChatMessageType`
- `AgentType` enum added to Agent model for event type resolution
- 11 new/modified files total:
  - Created: `SessionContext.cs`, `BrainMetadata.cs`, `ToolCallInfo.cs`
  - Created: `Timeline/EventOutputType.cs`, `ChessEventType.cs`, `RpgEventType.cs`, `FpsEventType.cs`
  - Created: `Timeline/TimelineEvent.cs`, `EventLine.cs`, `TimelineCheckpoint.cs`, `EventIconMap.cs`
  - Modified: `Agent.cs`, `ChatMessage.cs`, `AgentSelectionViewModel.cs`

---

## Files to Create

```
src/GaimerDesktop/GaimerDesktop/Models/
├── SessionContext.cs
├── BrainMetadata.cs
├── ToolCallInfo.cs
└── Timeline/
    ├── TimelineCheckpoint.cs
    ├── EventLine.cs
    ├── TimelineEvent.cs
    ├── EventOutputType.cs
    ├── ChessEventType.cs
    ├── RpgEventType.cs
    ├── FpsEventType.cs (placeholder)
    └── EventIconMap.cs
```

## Files to Modify

```
src/GaimerDesktop/GaimerDesktop/Models/Agent.cs
src/GaimerDesktop/GaimerDesktop/Models/ChatMessage.cs
src/GaimerDesktop/GaimerDesktop/ViewModels/AgentSelectionViewModel.cs (use Agents.Available)
```

---

## Icon Asset Mapping

> **Policy:** No icon reuse. Each event type has a unique asset.

### Generic Icons (All Agents) — 11 Assets

| EventOutputType | Asset | Description |
|-----------------|-------|-------------|
| Danger | `alert.png` | Warning triangle |
| Opportunity | `opportunity.png` | Hands + star |
| SageAdvice | `sage.png` | Wizard/sage figure |
| Assessment | `assessment.png` | Doc + magnifier |
| GameStateChange | `new-game.png` | Playing cards |
| Detection | `detection.png` | Eye/vision icon |
| DirectMessage | `chat.png` | Speech bubbles |
| ImageAnalysis | `video-reel.png` | Film reel |
| AnalyticsResult | `performance.png` | Chart + laptop |
| HistoryRecall | `history-clock.png` | Scroll + clock |
| GeneralChat | `call-out.png` | Megaphone |

### Chess Icons (Leroy) — 2 Assets

| ChessEventType | Asset | Description |
|----------------|-------|-------------|
| PositionEval | `chessboard-eval.png` | Knight + magnifier |
| Tactic | `chess-move.png` | Chess move indicator |

### RPG Icons (Derek) — 3 Assets

| RpgEventType | Asset | Description |
|--------------|-------|-------------|
| QuestHint | `quest.png` | Quest marker |
| Lore | `lore.png` | Scroll/book |
| ItemAlert | `item-alert.png` | Treasure chest/item |

---

## Notes

- Keep existing `DeliveryState` enum for user message status (Pending/Sent/Failed)
- `ChatMessage` remains for LLM history; `TimelineEvent` is the UI-focused wrapper
- `ObservableCollection` used for MVVM binding compatibility
- **Only Leroy (Chess) agent is available initially** — Derek and Wasp gated via `IsAvailable = false`
- FPS event types are placeholders for future implementation
- `SageAdvice` consolidates tactical guidance, best moves, and build advice into one generic type
- `Detection` is a user-primed vision capability — users can request "call out X if seen"
- No icon reuse policy ensures visual distinction for each event type
