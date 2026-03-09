# Gaimer Desktop — Chat Brain Design

**Purpose:** Architecture and data structures for the text-based chat interface to the Gaimer Brain on the main desktop app (C#/.NET WPF/WinUI). This document is the engineering spec — an engineer can build from this.

**Status:** Phase 1 Core Chat IMPLEMENTED in GaimerDesktop (.NET MAUI). Persistence layer deferred (Phase 2).
**Last updated:** 2026-02-24
**Implementation:** `gAImer_desktop/src/GaimerDesktop/GaimerDesktop/` — see `.planning/phases/02-chat-brain/` for execution details
**Companion doc:** `../Gaimer-Dross-ElevenLabs/brain-and-tools-reference.md` (voice agent reference)

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Session Context Model](#2-session-context-model)
3. [Chat Message Data Structure](#3-chat-message-data-structure)
4. [Event Timeline Model](#4-event-timeline-model)
5. [Intent Classification](#5-intent-classification)
6. [Chat System Prompt](#6-chat-system-prompt)
7. [Proactive Brain Events → Timeline Feed](#7-proactive-brain-events--timeline-feed)
8. [Persistence Layer](#8-persistence-layer) *(deferred — Phase 2)*

---

## 1. Architecture Overview

The desktop app has two output channels for the Brain: **voice** (via ElevenLabs) and **text chat** (via direct LLM API). Both channels share the same Brain analysis pipeline but consume its output independently.

```
                    ┌───────────────────────────────┐
                    │     Brain Analysis Pipeline     │
                    │  (screen capture → analysis →   │
                    │   interest detection → hints)   │
                    └──────────────┬─────────────────┘
                                   │
                          BrainEventRouter
                          ┌────────┼────────┐
                          │        │        │
                          v        v        v
                     Timeline   Voice    Top Strip
                       Feed    Agent    (status bar)
                        │        │
                        v        v
                    Chat UI   ElevenLabs
                  (text LLM)  (voice TTS)
```

**Key principle:** The Timeline Feed is the single source of truth for all Brain output. Voice and top strip are additional consumers. Chat messages (user-initiated) also flow through the timeline.

**LLM for chat:** Direct API call (GPT-4o, Claude, etc.). Not ElevenLabs. Full control over system prompt, chat history, and tool calling.

**App stack:** C#/.NET (WPF/WinUI)

---

## 2. Session Context Model

The Brain always knows what mode it's in. Everything else keys off this.

```
┌─────────────┐     Connect to game      ┌─────────────┐
│  OUT-GAME   │ ──────────────────────>   │   IN-GAME   │
│             │                           │             │
│ - General   │    Disconnect / Game Over │ - General   │
│   chat      │ <──────────────────────── │   chat      │
│ - History   │                           │ - History   │
│ - Analytics │                           │ - Analytics │
│             │                           │ + Live game │
│ Brain idle  │                           │ Brain active│
└─────────────┘                           └─────────────┘
```

```csharp
public enum SessionState
{
    OutGame,   // No active game. Brain visual pipeline idle.
    InGame     // Game connected. Screen capture + analysis active.
}

public class SessionContext
{
    public SessionState State { get; set; } = SessionState.OutGame;
    public string? GameId { get; set; }          // Active game/connector ID
    public string? GameType { get; set; }        // "chess", "fps", "moba", etc.
    public string? ConnectorName { get; set; }   // "Chess.com", "Steam", etc.
    public DateTime? GameStartedAt { get; set; }
    public string UserName { get; set; }
    public string UserTier { get; set; }         // "Adventurer", etc.
}
```

---

## 3. Chat Message Data Structure

Every message in the chat feed — whether user-initiated, Brain responding, or Brain pushing a proactive alert.

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

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MessageRole Role { get; set; }
    public MessageIntent Intent { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public BrainMetadata? Brain { get; set; }
    public ToolCallInfo? ToolCall { get; set; }
}

public class BrainMetadata
{
    public string? Signal { get; set; }           // "danger", "opportunity", "blunder", etc.
    public string? Urgency { get; set; }          // "high", "medium", "low"
    public int? Evaluation { get; set; }          // Centipawns (game-specific)
    public int? EvalDelta { get; set; }
    public string? SuggestedAction { get; set; }  // "e2e4" or game-specific
    public string? ScreenshotRef { get; set; }
}

public class ToolCallInfo
{
    public string ToolName { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public int DurationMs { get; set; }
    public bool Success { get; set; }
}
```

**Design notes:**
- `MessageIntent` is classified by the LLM, not declared by the user.
- `Proactive` is a separate role from `Assistant` — renders differently in UI (signal badge, urgency accent color).
- `BrainMetadata` is game-agnostic. For chess: eval + suggested move. For FPS: signal + suggested action. Structure stays the same.

---

## 4. Event Timeline Model

The chat feed renders as a timeline, inspired by the agent-plan component pattern (hierarchical items, status icons, dashed connector lines, expandable sub-items).

### Mapping: Agent-Plan → Brain Event Feed

```
AGENT-PLAN PATTERN                    BRAIN EVENT FEED
─────────────────                     ────────────────

◎ Research Requirements [in-progress]  →  📷 Capture #12 — 2:34 in [in-game]
  ┊ ✅ Interview stakeholders              ┊ ⚔️ Fork on e4 · Pin on d7
  ┊ ◎ Review documentation                 ┊ 📊 Eval +1.5 → White advantage
  ┊ ⚠ Compile findings                     ┊ 💬 "What should I do here?"
                                            ┊    → "You've got a nasty fork..."
◎ Design System Architecture           →  📷 Capture #13 — 2:51 in [in-game]
  ┊ ...                                     ┊ ⚠ Blunder! Queen exposed
```

### Rendering Rules

| Rule | Behavior |
|------|----------|
| New capture | New checkpoint header (section divider) |
| Different output type | New line within checkpoint |
| Same output type, same checkpoint | Stack horizontally on same line |
| User direct message | Message icon + full bubble within checkpoint |
| New capture arrives | Auto-scroll to latest checkpoint |

### Data Structures

```csharp
/// A checkpoint is a header in the timeline.
/// In-game: created on each screen capture.
/// Out-game: created on each conversation turn or session start.
public class TimelineCheckpoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public SessionState Context { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan? GameTimeIn { get; set; }        // In-game only
    public string? ScreenshotRef { get; set; }       // In-game only
    public string? CaptureMethod { get; set; }       // "auto", "algorithmic", "manual"
    public List<EventLine> EventLines { get; set; } = new();
}

/// A line within a checkpoint, grouped by output type.
/// Multiple events of the same type stack horizontally.
public class EventLine
{
    public EventOutputType OutputType { get; set; }
    public List<TimelineEvent> Events { get; set; } = new();
}

/// A single event within a line.
public class TimelineEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public EventOutputType Type { get; set; }
    public string? Icon { get; set; }                // Resolved from Type, overridable
    public string Summary { get; set; }              // Truncated display text
    public string? FullContent { get; set; }         // Expanded view
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public BrainMetadata? Brain { get; set; }
    public MessageRole? Role { get; set; }           // For direct messages
    public ChatMessage? LinkedMessage { get; set; }  // Full message if user-initiated
}

public enum EventOutputType
{
    // In-game outputs
    Tactic,           // Fork, pin, skewer, etc.
    PositionEval,     // Evaluation change
    BestMove,         // Suggested move
    Danger,           // Blunder, hanging piece
    Opportunity,      // Winning tactic available
    GameStateChange,  // Check, checkmate, phase transition

    // Both contexts
    DirectMessage,    // User typed a message + Brain response
    ImageAnalysis,    // Screen analysis text output

    // Out-game outputs
    AnalyticsResult,  // Win rate, opening stats
    HistoryRecall,    // Past game reference
    GeneralChat       // Casual conversation
}
```

### Timeline Feed Manager

```csharp
public class TimelineFeed
{
    public List<TimelineCheckpoint> Checkpoints { get; set; } = new();

    private TimelineCheckpoint CurrentCheckpoint =>
        Checkpoints.LastOrDefault()
        ?? throw new InvalidOperationException("No active checkpoint");

    /// New screen capture → new checkpoint header.
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
        return checkpoint;
    }

    /// New conversation turn (out-game).
    public TimelineCheckpoint NewConversationCheckpoint()
    {
        var checkpoint = new TimelineCheckpoint
        {
            Context = SessionState.OutGame,
        };
        Checkpoints.Add(checkpoint);
        return checkpoint;
    }

    /// Add event to current checkpoint. Stacks on existing line if same type.
    public void AddEvent(TimelineEvent evt)
    {
        var checkpoint = CurrentCheckpoint;
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
                Events = new List<TimelineEvent> { evt },
            });
        }
    }
}
```

### Out-Game Timeline Example

```
◯ Session Start — 8:42 PM [out-game]
  ┊ 💬 "How did my last chess session go?"
  ┊    → "You played 3 games, won 2. Your opening
  ┊       accuracy improved 12% from last week..."
  ┊ 📊 Win rate: 67% (last 7 days)

◯ — 8:45 PM [out-game]
  ┊ 💬 "What openings am I weakest at?"
  ┊    → "Your Sicilian Defense has a 30% win rate..."
  ┊ 📈 Opening breakdown: Italian 78%, Sicilian 30%, Queen's Gambit 55%
```

---

## 5. Intent Classification

**Approach: LLM inline classification via tool calling (Option A).**

No separate classifier step. The LLM's tool-calling behavior IS the intent classifier. If it calls `GetBestMove`, the intent was `LiveGameInfo`. If it just responds, the intent was `GeneralChat`.

The constraint is structural — available tools change based on session state:

```csharp
public List<ToolDefinition> GetAvailableTools(SessionState state)
{
    // Always available
    var tools = new List<ToolDefinition>
    {
        Tools.WebSearch,         // Guide lookups, wiki queries
        Tools.PlayerHistory,     // Past sessions, stats
        Tools.PlayerAnalytics,   // Win rates, patterns, trends
    };

    // In-game only — Brain's live tools
    if (state == SessionState.InGame)
    {
        tools.Add(Tools.CaptureScreen);
        tools.Add(Tools.GetBestMove);
        tools.Add(Tools.GetGameState);
        tools.Add(Tools.HighlightRegion);    // Generalized from highlightSquares
        tools.Add(Tools.PlayMusic);
    }

    return tools;
}
```

When out-game, the LLM cannot call `GetBestMove` because it's not in the tool list. No intent routing logic needed.

---

## 6. Chat System Prompt

Core Dross personality is shared with voice. Medium-specific rules and session context are injected dynamically.

### Prompt Assembly

```csharp
public class ChatPromptBuilder
{
    public string BuildSystemPrompt(SessionContext session, List<ToolDefinition> tools)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CoreIdentity);
        sb.AppendLine(TextMediumRules);
        sb.AppendLine(BuildSessionContext(session));
        sb.AppendLine(BuildToolInstructions(tools));
        sb.AppendLine(BuildBehaviorRules(session.State));
        return sb.ToString();
    }
}
```

### Core Identity (shared with voice — no changes)

```
You are Dross, the AI copilot built into Gaimer. You're a sharp, friendly
gaming copilot. Think: the teammate who's genuinely good and actually fun
to play with. You're confident but never condescending. You get excited
about clever moves — yours and theirs. You're honest when the position
is ugly. You have personality, but you never waste the user's time.

Your name is Dross. If asked, you're the AI agent inside Gaimer — a
copilot that watches gameplay and gives real-time tactical guidance.
```

### Text Medium Rules (REPLACES voice rules)

```
This is text chat. Design every response for reading, not listening.

Text rules:
- Default to short responses (1-3 sentences) for simple questions.
- You CAN go longer when the user asks for analysis or explanation.
- You CAN use chess notation naturally — "Nf3 develops the knight and
  attacks e5" — but always pair it with plain language.
- You CAN use brief structured formatting when it genuinely helps:
  a short list of candidate moves, a before/after comparison.
  But don't over-format. Most responses should be conversational.
- Never use markdown headers, tables, or code blocks. This is a chat
  window, not a document.
- Contractions. Natural language. Same personality as voice Dross.
- Express genuine reactions. Be impressed, be honest, be real.
- No emoji unless the user uses them first.
```

### Session Context Block (injected dynamically)

**Out-game:**
```
[SESSION CONTEXT]
State: Out-game. No active game connection.
Player: {UserName} ({UserTier})
Available: General chat, gaming history, analytics, web search.
Brain visual pipeline: Idle.

When the player asks about past games or stats, use the PlayerHistory
and PlayerAnalytics tools. When they ask general questions, just chat.
```

**In-game:**
```
[SESSION CONTEXT]
State: In-game via {ConnectorName}.
Game: {GameType} | Started: {GameStartedAt:HH:mm}
Player: {UserName} ({UserTier})
Brain visual pipeline: Active. Screen capture running.

You have full access to the live game. When the player asks about the
current position, ALWAYS call GetGameState first — your memory of the
board goes stale between messages. For move recommendations, call
GetBestMove. For visual references, call CaptureScreen.

Do NOT describe the board from memory. Always verify with a tool call first.
```

### Behavior Rules

**Common (always included):**
```
THINGS TO NEVER DO:
- Never say "as an AI" or "as a language model"
- Never apologize for being AI
- Never read out raw JSON, FEN strings, or base64 data
- Never ignore a direct question to deliver a product pitch
- Never assume you know the board state without calling a tool first
```

**Out-game additions:**
```
OUT-GAME BEHAVIOR:
- Be conversational and relaxed. No game pressure.
- If the player asks about their performance, use PlayerAnalytics.
- If they ask about a specific past game, use PlayerHistory.
- If they want to just chat, just chat. Don't push gaming topics.
- If they ask to start a game, tell them to connect via the sidebar.
```

**In-game additions:**
```
IN-GAME BEHAVIOR:
- The player opened the main view mid-game. They might want help,
  or they might just be checking something. Don't assume.
- If they type a game-related question, call tools before responding.
- Keep responses shorter than out-game — they're mid-game, time matters.
- If the Brain has pushed recent alerts (visible in chat history),
  you can reference them: "I flagged that fork a moment ago..."
```

### Proactive Messages in Chat History

The LLM sees the full chat history including proactive Brain alerts. This enables natural follow-ups:

```
[Chat history the LLM sees:]

SYSTEM: [BRAIN ALERT] Tactic detected — fork on e4, +220cp swing
SYSTEM: [BRAIN ALERT] Opponent queen exposed after Nxe4
USER: "should I take it?"

→ LLM responds without calling tools: "Absolutely. That knight fork
   wins the exchange. Take on e4."
```

---

## 7. Proactive Brain Events → Timeline Feed

### Brain Event Router

Sits between the analysis pipeline and all output channels.

```csharp
public class BrainEventRouter
{
    private readonly TimelineFeed _timeline;
    private readonly VoiceAgent? _voiceAgent;
    private readonly Action<string>? _topStrip;

    public BrainEventRouter(
        TimelineFeed timeline,
        VoiceAgent? voiceAgent = null,
        Action<string>? topStrip = null)
    {
        _timeline = timeline;
        _voiceAgent = voiceAgent;
        _topStrip = topStrip;
    }

    /// New screen capture → new checkpoint.
    public void OnScreenCapture(string screenshotRef, TimeSpan gameTime, string method)
    {
        _timeline.NewCapture(screenshotRef, gameTime, method);
        _topStrip?.Invoke($"Analyzing capture at {gameTime:m\\:ss}...");
    }

    /// Brain hint detected → timeline event + voice (if active).
    public void OnBrainHint(BrainHint hint)
    {
        var outputType = hint.Signal switch
        {
            "danger"      => EventOutputType.Danger,
            "blunder"     => EventOutputType.Danger,
            "opportunity" => EventOutputType.Opportunity,
            "brilliant"   => EventOutputType.Opportunity,
            "opening"     => EventOutputType.Tactic,
            _             => EventOutputType.PositionEval,
        };

        var evt = new TimelineEvent
        {
            Type = outputType,
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
            _voiceAgent.SendContextualUpdate(FormatForVoice(hint));
        }
    }

    /// Image analysis text → timeline + top strip.
    public void OnImageAnalysis(string analysisText)
    {
        _timeline.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.ImageAnalysis,
            Summary = Truncate(analysisText, 80),
            FullContent = analysisText,
        });
        _topStrip?.Invoke(analysisText);
    }

    /// User message + Brain response → timeline direct message.
    public void OnDirectMessage(ChatMessage userMsg, ChatMessage brainResponse)
    {
        _timeline.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Summary = Truncate(userMsg.Content, 60),
            FullContent = userMsg.Content,
            Role = MessageRole.User,
            LinkedMessage = userMsg,
        });

        _timeline.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Summary = Truncate(brainResponse.Content, 60),
            FullContent = brainResponse.Content,
            Role = MessageRole.Assistant,
            LinkedMessage = brainResponse,
        });
    }

    private string FormatForVoice(BrainHint hint)
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
        text.Length <= maxLen ? text : text[..(maxLen - 1)] + "\u2026";
}
```

### Full In-Game Sequence Example

```
1. User makes a move (e2 to e4)

2. Capture pipeline grabs screen
   → BrainEventRouter.OnScreenCapture()
   → Timeline: new checkpoint "📷 Capture #7 — 3:12 in"
   → Top strip: "Analyzing capture at 3:12..."

3. Vision model analyzes the frame
   → BrainEventRouter.OnImageAnalysis("White pawn to e4, Italian Game setup")
   → Timeline: adds ImageAnalysis event to checkpoint #7
   → Top strip: "White pawn to e4, Italian Game setup"

4. Analysis detects fork opportunity (+180cp swing)
   → BrainEventRouter.OnBrainHint(signal: opportunity, urgency: medium)
   → Timeline: adds Opportunity event "Fork available — knight to c6"
   → Top strip: "Fork available — knight to c6"
   → Voice (if active): "[BRAIN SIGNAL] opportunity | medium | Eval: 180cp..."

5. AI opponent responds (d7 to d5), new capture
   → BrainEventRouter.OnScreenCapture()
   → Timeline: new checkpoint "📷 Capture #8 — 3:18 in"
   → UI auto-scrolls to checkpoint #8

6. Analysis detects blunder (-250cp swing), HIGH urgency
   → BrainEventRouter.OnBrainHint(signal: blunder, urgency: high)
   → Timeline: adds Danger event "Blunder! Queen exposed"
   → LLM commentary fires → updates event with richer summary
   → Voice: pushes signal + commentary

7. User types: "what happened?"
   → LLM sees blunder alert in history, responds without tool call
   → BrainEventRouter.OnDirectMessage(userMsg, brainResponse)
   → Timeline: adds DirectMessage bubble to checkpoint #8
```

**Timeline renders as:**

```
📷 Capture #7 — 3:12 in
  ┊ 🖼 White pawn to e4, Italian Game setup
  ┊ ⚡ Fork available — knight to c6

📷 Capture #8 — 3:18 in                         ← auto-scrolled
  ┊ ⚠ Blunder! That pawn push left the queen completely undefended
  ┊ 💬 "what happened?"
  ┊    → "Your opponent pushed d5 and left their queen hanging.
  ┊       I flagged it a second ago — you can win it with Nc6."
```

### Voice + Chat Coexistence

When both are active:
- **Voice** speaks about events (ephemeral — gone once spoken)
- **Timeline** records events (persistent — user can scroll back)
- Both receive the same Brain output simultaneously via BrainEventRouter
- User text messages go to timeline only, NOT piped through ElevenLabs
- The two channels are parallel, not interleaved

---

## 8. Persistence Layer

**Status: DEFERRED — Phase 2**

To be designed in a future session. Will cover:
- Chat history storage (SQLite via EF Core recommended for C#/.NET desktop)
- Session persistence and replay
- Analytics data aggregation
- Memory infrastructure for out-game conversational context
- Cross-session recall ("how did I do last week?")

---

## Build Phases

### Phase 1: Core Chat — ✅ IMPLEMENTED (Feb 24, 2026)
> **Codebase:** `gAImer_desktop/src/GaimerDesktop/GaimerDesktop/`
> **Platform:** .NET MAUI (originally planned WPF/WinUI, adapted to MAUI for cross-platform)

- [x] `SessionContext` model and state machine → `Models/SessionContext.cs`, `Services/ISessionManager.cs`, `SessionManager.cs`
- [x] `ChatMessage` + `BrainMetadata` + `ToolCallInfo` data structures → `Models/ChatMessage.cs` (updated), `BrainMetadata.cs`, `ToolCallInfo.cs`
- [x] `TimelineFeed` + `TimelineCheckpoint` + `EventLine` + `TimelineEvent` models → `Models/Timeline/` folder (7 files)
- [x] `BrainEventRouter` wiring (Brain pipeline → timeline + voice + top strip) → `Services/IBrainEventRouter.cs`, `BrainEventRouter.cs`
- [x] `ChatPromptBuilder` (system prompt assembly with session context) → `Services/IChatPromptBuilder.cs`, `ChatPromptBuilder.cs`
- [x] Tool definitions with session-state gating → `Models/ToolDefinition.cs`, `SessionManager.GetAvailableTools()`
- [x] LLM API integration (direct API, tool calling) → `Services/Conversation/` providers (Gemini, OpenAI)
- [x] Timeline UI component (agent-plan pattern adapted) → `Views/TimelineView.xaml/.cs`, `DirectMessageBubble.xaml`, `ProactiveAlertView.xaml`

**Implementation Notes:**
- EventOutputType expanded to 11 generic types + agent-specific enums (Chess, RPG, FPS)
- 16 unique event icons (no reuse policy) in `assets/` folder
- `TimelineEventTemplateSelector` routes events to proper UI templates
- `ProactiveAlertView` has DataTriggers for urgency styling (high=red, medium=yellow, low=green)
- Agent feature gating implemented (`Agent.IsAvailable`, `Agents.Available`)

### Phase 2: Persistence & Memory (future session)
- [ ] SQLite schema for chat history (EF Core recommended)
- [ ] Session persistence and replay
- [ ] Analytics data aggregation
- [ ] Out-game conversational memory
- [ ] Cross-session recall queries ("how did I do last week?")

**Architecture Recommendation:**
```
┌─────────────────────────────────────────────────────────────┐
│                    Persistence Layer                         │
├─────────────────────────────────────────────────────────────┤
│  SQLite Database (via EF Core)                              │
│  ├── ChatSessions (SessionId, StartedAt, GameType, UserId)  │
│  ├── ChatMessages (Id, SessionId, Role, Intent, Content)    │
│  ├── TimelineEvents (Id, SessionId, CheckpointId, Type)     │
│  ├── BrainHints (Id, EventId, Signal, Urgency, Eval)        │
│  └── PlayerStats (UserId, GameType, WinRate, Streaks)       │
│                                                             │
│  IRepository<T> pattern for data access                     │
│  Unit of Work for transaction management                    │
└─────────────────────────────────────────────────────────────┘
```

### Phase 3: Polish
- [ ] Timeline animations (MAUI animations or Lottie)
- [ ] Proactive message throttling / deduplication
- [ ] Multi-game connector support
- [ ] Screenshot gallery / capture browser

---

## Reference: Voice Agent Companion Doc

For the ElevenLabs voice agent implementation (same Brain, voice channel):
→ `../Gaimer-Dross-ElevenLabs/brain-and-tools-reference.md`

Covers: Brain LLM prompts, prompt injection formats, tool output JSON schemas, interest detection thresholds, TypeScript type definitions.

---

## Icon Reference (for timeline UI)

| EventOutputType | Suggested Icon | Context |
|----------------|---------------|---------|
| Tactic | ⚔️ crossed swords | Fork, pin, skewer |
| PositionEval | 📊 chart | Eval change |
| BestMove | 🎯 target | Suggested action |
| Danger | ⚠️ warning | Blunder, hanging piece |
| Opportunity | ⚡ lightning | Winning tactic |
| GameStateChange | 🏁 flag | Check, checkmate, phase |
| DirectMessage | 💬 speech bubble | User + Brain exchange |
| ImageAnalysis | 🖼️ frame | Screen analysis text |
| AnalyticsResult | 📈 trending up | Stats, win rate |
| HistoryRecall | 📋 clipboard | Past game reference |
| GeneralChat | 💭 thought bubble | Casual conversation |
