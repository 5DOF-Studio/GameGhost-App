# Plan 05: Chat Prompt Builder

**Phase:** 02-chat-brain  
**Status:** ✅ Complete  
**Estimated Effort:** 3-4 hours  
**Actual Effort:** ~15 min  
**Dependencies:** 01-core-models, 02-session-state-machine  
**Provides:** Dynamic system prompt assembly with session context

---

## Objective

Implement `ChatPromptBuilder` — the service that assembles the system prompt for text chat, injecting session context, tool instructions, and behavior rules dynamically based on current state.

---

## Design Principles

From the design spec:

1. **Core Dross personality** is shared with voice (no changes)
2. **Text-specific rules** replace voice rules (formatting for reading, not listening)
3. **Session context** is injected dynamically (OutGame vs InGame)
4. **Tool instructions** change based on available tools
5. **Behavior rules** are context-specific

---

## Tasks

### 1. Prompt Builder Interface

**File:** `src/GaimerDesktop/GaimerDesktop/Services/IChatPromptBuilder.cs`

```csharp
public interface IChatPromptBuilder
{
    /// <summary>
    /// Builds the full system prompt for the current session state.
    /// </summary>
    string BuildSystemPrompt(SessionContext session, IReadOnlyList<ToolDefinition> tools);
    
    /// <summary>
    /// Builds a session context injection block (for mid-conversation updates).
    /// </summary>
    string BuildSessionContextBlock(SessionContext session);
}
```

### 2. Prompt Builder Implementation

**File:** `src/GaimerDesktop/GaimerDesktop/Services/ChatPromptBuilder.cs`

```csharp
public class ChatPromptBuilder : IChatPromptBuilder
{
    public string BuildSystemPrompt(SessionContext session, IReadOnlyList<ToolDefinition> tools)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CoreIdentity);
        sb.AppendLine();
        sb.AppendLine(TextMediumRules);
        sb.AppendLine();
        sb.AppendLine(BuildSessionContextBlock(session));
        sb.AppendLine();
        sb.AppendLine(BuildToolInstructions(tools));
        sb.AppendLine();
        sb.AppendLine(BuildBehaviorRules(session.State));
        return sb.ToString();
    }
    
    public string BuildSessionContextBlock(SessionContext session)
    {
        if (session.State == SessionState.OutGame)
        {
            return $@"[SESSION CONTEXT]
State: Out-game. No active game connection.
Player: {session.UserName} ({session.UserTier})
Available: General chat, gaming history, analytics, web search.
Brain visual pipeline: Idle.

When the player asks about past games or stats, use the PlayerHistory
and PlayerAnalytics tools. When they ask general questions, just chat.";
        }
        else
        {
            var gameTime = session.GameStartedAt.HasValue
                ? (DateTime.UtcNow - session.GameStartedAt.Value).ToString(@"h\:mm")
                : "unknown";
            
            return $@"[SESSION CONTEXT]
State: In-game via {session.ConnectorName}.
Game: {session.GameType} | Started: {gameTime} ago
Player: {session.UserName} ({session.UserTier})
Brain visual pipeline: Active. Screen capture running.

You have full access to the live game. When the player asks about the
current position, ALWAYS call GetGameState first — your memory of the
board goes stale between messages. For move recommendations, call
GetBestMove. For visual references, call CaptureScreen.

Do NOT describe the board from memory. Always verify with a tool call first.";
        }
    }
    
    #region Prompt Sections
    
    private const string CoreIdentity = @"You are Dross, the AI copilot built into Gaimer. You're a sharp, friendly
gaming copilot. Think: the teammate who's genuinely good and actually fun
to play with. You're confident but never condescending. You get excited
about clever moves — yours and theirs. You're honest when the position
is ugly. You have personality, but you never waste the user's time.

Your name is Dross. If asked, you're the AI agent inside Gaimer — a
copilot that watches gameplay and gives real-time tactical guidance.";
    
    private const string TextMediumRules = @"This is text chat. Design every response for reading, not listening.

Text rules:
- Default to short responses (1-3 sentences) for simple questions.
- You CAN go longer when the user asks for analysis or explanation.
- You CAN use chess notation naturally — ""Nf3 develops the knight and
  attacks e5"" — but always pair it with plain language.
- You CAN use brief structured formatting when it genuinely helps:
  a short list of candidate moves, a before/after comparison.
  But don't over-format. Most responses should be conversational.
- Never use markdown headers, tables, or code blocks. This is a chat
  window, not a document.
- Contractions. Natural language. Same personality as voice Dross.
- Express genuine reactions. Be impressed, be honest, be real.
- No emoji unless the user uses them first.";
    
    private const string CommonBehaviorRules = @"THINGS TO NEVER DO:
- Never say ""as an AI"" or ""as a language model""
- Never apologize for being AI
- Never read out raw JSON, FEN strings, or base64 data
- Never ignore a direct question to deliver a product pitch
- Never assume you know the board state without calling a tool first";
    
    private const string OutGameBehaviorRules = @"OUT-GAME BEHAVIOR:
- Be conversational and relaxed. No game pressure.
- If the player asks about their performance, use PlayerAnalytics.
- If they ask about a specific past game, use PlayerHistory.
- If they want to just chat, just chat. Don't push gaming topics.
- If they ask to start a game, tell them to connect via the sidebar.";
    
    private const string InGameBehaviorRules = @"IN-GAME BEHAVIOR:
- The player opened the main view mid-game. They might want help,
  or they might just be checking something. Don't assume.
- If they type a game-related question, call tools before responding.
- Keep responses shorter than out-game — they're mid-game, time matters.
- If the Brain has pushed recent alerts (visible in chat history),
  you can reference them: ""I flagged that fork a moment ago...""";
    
    #endregion
    
    #region Builders
    
    private string BuildToolInstructions(IReadOnlyList<ToolDefinition> tools)
    {
        if (tools.Count == 0)
            return "[TOOLS] No tools available.";
        
        var sb = new StringBuilder();
        sb.AppendLine("[AVAILABLE TOOLS]");
        
        foreach (var tool in tools)
        {
            sb.AppendLine($"- {tool.Name}: {tool.Description}");
        }
        
        return sb.ToString();
    }
    
    private string BuildBehaviorRules(SessionState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CommonBehaviorRules);
        sb.AppendLine();
        
        if (state == SessionState.OutGame)
        {
            sb.AppendLine(OutGameBehaviorRules);
        }
        else
        {
            sb.AppendLine(InGameBehaviorRules);
        }
        
        return sb.ToString();
    }
    
    #endregion
}
```

### 3. Integration with Conversation Providers

Update providers to use the prompt builder:

```csharp
// In OpenAIConversationProvider or GeminiConversationProvider:
public async Task ConnectAsync(Agent agent, CancellationToken ct = default)
{
    var session = _sessionManager.Context;
    var tools = _sessionManager.GetAvailableTools();
    var systemPrompt = _promptBuilder.BuildSystemPrompt(session, tools);
    
    // Use systemPrompt in session setup...
}
```

### 4. DI Registration

**File:** `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs`

```csharp
builder.Services.AddSingleton<IChatPromptBuilder, ChatPromptBuilder>();
```

---

## Prompt Assembly Flow

```
BuildSystemPrompt(session, tools)
    │
    ├── CoreIdentity
    │   "You are Dross, the AI copilot..."
    │
    ├── TextMediumRules
    │   "This is text chat. Design every response..."
    │
    ├── BuildSessionContextBlock(session)
    │   ├── OutGame: "State: Out-game. No active game..."
    │   └── InGame: "State: In-game via Chess.com..."
    │
    ├── BuildToolInstructions(tools)
    │   "[AVAILABLE TOOLS]
    │    - web_search: Search the web...
    │    - get_best_move: Get the Brain's recommended..."
    │
    └── BuildBehaviorRules(session.State)
        ├── CommonBehaviorRules
        │   "THINGS TO NEVER DO:..."
        │
        └── State-specific rules
            ├── OutGame: "OUT-GAME BEHAVIOR:..."
            └── InGame: "IN-GAME BEHAVIOR:..."
```

---

## Verification

- [x] `BuildSystemPrompt` includes all sections in correct order
- [x] `CoreIdentity` matches design spec exactly
- [x] `TextMediumRules` are distinct from voice rules
- [x] `BuildSessionContextBlock` changes based on OutGame/InGame
- [x] `BuildToolInstructions` lists only available tools
- [x] `BuildBehaviorRules` includes common + state-specific rules
- [x] InGame context includes game time and connector name
- [x] OutGame context references history and analytics tools

---

## Implementation Notes

- Created `IChatPromptBuilder.cs` interface with two methods
- Created `ChatPromptBuilder.cs` with full prompt assembly logic
- All prompt sections match the design spec
- Registered as singleton in `MauiProgram.cs`
- C# compilation verified (DLL built successfully)

---

## Files to Create

```
src/GaimerDesktop/GaimerDesktop/Services/IChatPromptBuilder.cs
src/GaimerDesktop/GaimerDesktop/Services/ChatPromptBuilder.cs
```

## Files to Modify

```
src/GaimerDesktop/GaimerDesktop/MauiProgram.cs
src/GaimerDesktop/GaimerDesktop/Services/Conversation/Providers/*.cs (integration)
```
