# Claude Code Integration Spec — Gaimer Team

**Status:** Draft (Channels-first revision)
**Created:** 2026-04-01
**Revised:** 2026-04-06
**Author:** Brainstorm session (PM + Claude)

---

## 1. Vision

Gaimer's gaming agents (Leroy, Wasp, RASA) are the user's sole companion during gameplay. When they encounter tasks beyond local context — web research, highlight reels, strategy guides, file operations, streaming control — they escalate to **Gaimer Team**, a background capability powered by Claude Code.

Gaimer Team is never user-facing. It has no voice, no personality, no agent card. The gaming agent says "I'm handing that to the team," continues the gaming session uninterrupted, and narrates the result when it arrives.

The user never talks to Gaimer Team directly. They talk to their agent, and their agent has a team behind it.

**The key architectural insight: the user's machine is the computer. Their Claude Code config is their tool ecosystem. Gaimer is just the voice trigger and gaming-context layer.** Claude Code already has file ops, bash, web search, MCP servers, plugins — Gaimer doesn't re-provide any of it. It sends intents and receives results.

The transport is **Claude Code Channels** — Gaimer ships a custom channel plugin that bridges the gaming companion to a running Claude Code session via named pipes. The user connects through ConnectorCards in the Gaimer UI — either launching a new Claude Code session or connecting to an existing one.

## 2. Key Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | Gaimer Team is a background service, not a voice agent | Oracle-class tasks are async. No personality, no voice, no agent card. Gaming agents relay results. |
| D2 | Gaming agents own escalation via "Gaimer Team" keyword | Agent self-classifies what it can't handle locally. User can also explicitly request team. No separate classifier or intent router. |
| D3 | Fire-and-forget with callback notification | Submit task → agent tells user "team's on it" → gaming continues → result arrives → agent narrates. No blocking. |
| D4 | Primary transport is Claude Code Channels | Custom Gaimer Channel Plugin (MCP server declaring `claude/channel`). Named pipe IPC between Gaimer and plugin. |
| D5 | Gaming agents keep their own fast tools | Quick lookups (Stockfish, brain analysis, journal) stay on the gaming agent. Gaimer Team handles deep research, multi-step reasoning, file operations — tasks taking 10+ seconds. |
| D6 | Gaimer is a thin pipe to Claude Code's full ecosystem | Claude Code already has file ops, bash, web search, MCP servers. Gaimer sends intents, receives results. Channel plugin is ~200 lines — dumb pipe, no business logic. |
| D7 | CLAUDE.md templates extend capabilities | Community writes CLAUDE.md overlay templates (OBS, Twitch, video pipelines). Claude Code's existing tool ecosystem does the rest. No custom tool bundles needed. |
| D8 | Auth is Claude Code's problem, not Gaimer's | User authenticates via `claude auth login` (browser OAuth). Gaimer triggers this during setup but never touches credentials. |
| D9 | Results flow through existing pipeline | Voice (TTS), timeline, ghost card. No new UI surface for results. Gaming agent routes Gaimer Team results through the same pipeline as brain results. |
| D10 | Interface is `IGaimerTeamService` | Fire-and-forget semantics. Callback/event for completion. Connection via named pipe to channel plugin. |
| D11 | Channel plugin bundled in Gaimer repo | Ship bundled for zero-friction. User never installs it manually. Publish to Claude Code plugin marketplace when protocol stabilizes. |
| D12 | Layered CLAUDE.md — core + overlays | Gaimer owns a core CLAUDE.md (protocol rules, safety scoping — inviolate, never user-edited). Users add overlay templates for personalization. Community extends via overlays, not code. |
| D13 | Process lifecycle: launch or discover | ConnectorCards UI. User can launch a new Claude Code session or connect to an existing one. Gaimer manages process health when it launches. |
| D14 | Named pipes / Unix domain sockets for IPC | macOS: Unix domain socket. Windows: named pipes. No network exposure, file-system permissions, lower latency than WebSocket. Channel plugin's stdio is reserved for MCP transport to Claude Code. |

## 3. Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│  User's Machine                                                   │
│                                                                   │
│  ┌───────────────────┐         ┌──────────────────────────────┐  │
│  │  Gaimer            │         │  Channel Plugin (Node/Bun)   │  │
│  │  (.NET MAUI)       │  named  │                              │  │
│  │                    │  pipe   │  - Pipe listener              │  │
│  │  Gaming Agents ────┤────────►│  - MCP notification fwd      │  │
│  │  (RASA/Leroy/Wasp) │         │  - Response relay             │  │
│  │                    │◄────────┤  - ~200 lines, dumb pipe     │  │
│  │  IGaimerTeamService│         │                              │  │
│  │                    │         │      ↕ MCP stdio             │  │
│  │  Voice ◄──┐        │         └──────────┬───────────────────┘  │
│  │  Brain    │        │                    │                      │
│  │  Timeline │        │         ┌──────────┴───────────────────┐  │
│  │  Ghost    │        │         │  Claude Code                  │  │
│  │           │        │         │                               │  │
│  └───────────┘        │         │  CLAUDE.md (core + overlays)  │  │
│                       │         │  Tools: bash, fs, web search  │  │
│            Gaming agent         │  MCP servers (user's config)  │  │
│            narrates result      │  Plugins (user's ecosystem)   │  │
│                                 └───────────────┬───────────────┘  │
│                                                 │ Anthropic API    │
└─────────────────────────────────────────────────┼──────────────────┘
                                                  ▼
                                         ┌──────────────┐
                                         │  Anthropic    │
                                         │  Cloud        │
                                         └──────────────┘
```

### Separation of Concerns

| Component | Responsibility |
|-----------|---------------|
| **Gaming Agent** (RASA/Leroy/Wasp) | Voice companion, game commentary, escalation decision, result narration |
| **IGaimerTeamService** | Task submission, named pipe client, process lifecycle, ConnectorCard state |
| **Channel Plugin** | Dumb pipe — named pipe listener ↔ MCP notification forwarder. No business logic. |
| **Claude Code** | Task reasoning, tool use, web search, file ops — full ecosystem. Governed by CLAUDE.md. |
| **CLAUDE.md Core** | Gaimer protocol rules, safety scoping, response format. Owned by Gaimer, never edited by user. |
| **CLAUDE.md Overlays** | User workflows — OBS, Twitch, video pipelines, streaming. Community-extensible. |
| **Brain/Voice/Timeline** | Unchanged — gaming agent routes Gaimer Team results through existing pipeline |

### What's New vs Unchanged

| Existing System | Impact |
|----------------|--------|
| IConversationProvider (Voice) | UNCHANGED — voice chat stays on OpenAI Realtime |
| IBrainService (Vision) | UNCHANGED — visual analysis stays on OpenRouter/Gemini |
| Brain-Voice Pipeline Rules | UNCHANGED — brain is sole image consumer, voice gets text only |
| ConnectorCards | EXTENDED — new card type for Claude Code session connection |
| **IGaimerTeamService** | NEW — async task submission to Claude Code via Channels |

## 4. Configuration, Auth & Connection Flow

### Feature Configuration (Settings → Team)

```
Settings
└── Team
    ├── Provider: [Claude Code ▼]  (future: Codex, etc.)
    │
    └── When "Claude Code" selected:
        ├── Installation check → install prompt if missing
        ├── Authentication → browser OAuth flow
        └── Status: ✓ Configured / ✗ Not configured
```

If a user tries to activate the Claude Code ConnectorCard from MainView without configuring first:

```
Alert: "Configure Claude Code in Settings → Team"
       [Open Settings]  [Cancel]
```

### Pre-flight Checks (triggered on provider selection)

1. **Claude Code installed?** — Check if `claude` CLI exists on PATH. If missing: show install prompt with download link. Re-check after user returns.
2. **Claude Code authenticated?** — Run `claude auth status` (or equivalent). If not authenticated: "Sign in to Claude" button triggers `claude auth login` → opens browser for OAuth. Gaimer polls/waits for auth completion.
3. **Both pass** → Feature status: ✓ Configured. ConnectorCard unlocked on MainView.

Auth persistence: Claude Code manages its own tokens. If auth expires mid-session, connection fails → Gaimer detects, shows "Re-authenticate" prompt on ConnectorCard. Don't re-check on every app launch — only on first enable and auth-related connection failures.

### Configuration State

| State | Settings shows | ConnectorCard shows |
|-------|---------------|-------------------|
| No provider selected | Provider dropdown, empty | Disabled or hidden |
| Claude Code selected, not installed | Install prompt + link | Alert → "Configure in Settings" |
| Claude Code selected, not authenticated | Authenticate button | Alert → "Configure in Settings" |
| Claude Code configured | ✓ Configured | **Enabled** — [New Session] [Connect Existing] |
| Connected | ✓ Configured, Connected | ● Connected, [Disconnect] |

### ConnectorCard — Session Lifecycle

**New Session flow:**
1. User taps "New Session" on ConnectorCard
2. Gaimer spawns Claude Code as a managed background process:
   `claude --channels {plugin_path} --dangerously-skip-permissions`
3. Channel plugin starts, creates named pipe/socket at known path
4. Gaimer connects to pipe, sends health ping
5. Claude Code responds → ConnectorCard status: **Connected**
6. Gaming agent: "Team's online."

**Connect Existing flow:**
1. User taps "Connect Existing"
2. Gaimer checks for an active named pipe/socket at the known path
3. If found → sends health ping → ConnectorCard status: **Connected**
4. If not found → "No active session detected. Launch a new one?"

**Known socket paths:**
- macOS: `~/Library/Application Support/Gaimer/gaimer-team.sock`
- Windows: `\\.\pipe\gaimer-team`

### Process Health Management (Gaimer-launched session)

| Event | Gaimer's Response |
|-------|-------------------|
| Claude Code process exits | Attempt restart (max 3). After 3 failures → Disconnected, agent: "Team's offline right now." |
| Named pipe connection drops | Reconnect with 2s backoff. If pipe path gone, treat as process exit. |
| No response to health ping (30s) | Mark unhealthy. Retry ping. After 3 missed pings, restart process. |
| User disconnects via ConnectorCard | Graceful shutdown — close pipe, terminate Claude Code process. |
| Gaimer app closes | Terminate owned Claude Code process on app exit. |

### Process Health Management (connected to existing session)

| Event | Gaimer's Response |
|-------|-------------------|
| Pipe connection drops | Disconnected. "Session ended. Launch a new one?" |
| No response to health ping | Same — offer to launch new session. |
| User disconnects | Close pipe only. Do NOT terminate Claude Code (Gaimer doesn't own it). |

## 5. Message Flow

The channel plugin is a dumb pipe. The message format is what Gaimer writes to the named pipe and what it reads back.

### Task Request (Gaimer → Claude Code)

```json
{
  "type": "task_request",
  "id": "task_a1b2c3",
  "timestamp": "2026-04-06T21:30:00Z",
  "task": "What's the best loadout for Shoothouse right now?",
  "context": {
    "game": "Call of Duty",
    "agent": "RASA",
    "session_id": "s_abc123",
    "recent_activity": "User just finished a 12-kill game on Shoothouse. Running MP5 build.",
    "l1_context": "Shoothouse HC Cyber Attack. User is top fragger.",
    "l2_context": "3 games this session, averaging 1.8 K/D."
  },
  "response_format": "voice"
}
```

### Task Result (Claude Code → Gaimer)

```json
{
  "type": "task_result",
  "id": "task_a1b2c3",
  "status": "complete",
  "response": "The meta build for Shoothouse right now is the MCW with JAK Heretic carbine kit. Faster TTK than your MP5 at every range on that map. Want the full attachment list?",
  "actions_taken": ["web_search", "file_read: game-packs/cod-hc-cyber-attack/meta.md"],
  "follow_up": "I can also check what the pros are running if you want.",
  "artifacts": []
}
```

### Status Update (Claude Code → Gaimer, optional)

```json
{
  "type": "status_update",
  "id": "task_a1b2c3",
  "status": "in_progress",
  "message": "Found 3 sources on current meta. Comparing builds."
}
```

### Error (Claude Code → Gaimer)

```json
{
  "type": "error",
  "id": "task_a1b2c3",
  "status": "error",
  "response": "Couldn't find recent patch data. The API might be down.",
  "error_code": "tool_failure"
}
```

### Permission Request (Claude Code → Gaimer)

```json
{
  "type": "permission_request",
  "id": "perm_xyz",
  "task_id": "task_a1b2c3",
  "action": "Delete 3 replay segments older than 24h",
  "risk": "low",
  "timeout_seconds": 60
}
```

### Permission Response (Gaimer → Claude Code)

```json
{
  "type": "permission_response",
  "id": "perm_xyz",
  "approved": true
}
```

### Health Check

```json
// Gaimer → Plugin
{ "type": "ping" }

// Plugin → Gaimer
{ "type": "pong" }
```

### Design Notes

- No `"protocol": "gaimer/v1"` header — the named pipe IS the protocol boundary. Unnecessary overhead removed.
- Context is text-based (`l1_context`, `l2_context`, `recent_activity`), not structured game state. Claude Code needs narrative context for research tasks, not FEN strings.
- `response_format` tells Claude Code whether to write 2-3 sentence voice answer or longer detailed response.
- `artifacts` array is kept for URLs, code blocks, data. Gaming agent decides what to surface in timeline vs narrate.

### What Gaimer Does NOT Send

- Raw screen captures (brain pipeline rule: brain is sole image consumer)
- User credentials or personal data
- Full conversation history (only recent context summaries)
- Structured game state objects (Claude Code gets text summaries)

### Permission Request Defense-in-Depth

Three layers coexist to minimize permission friction:

```
Layer 1: --dangerously-skip-permissions   (blocks most prompts)
Layer 2: CLAUDE.md Core safety scoping    (catches more via rules)
Layer 3: Permission Request UI            (handles whatever gets through)
```

`--dangerously-skip-permissions` reduces but does not eliminate permission prompts. CLAUDE.md scoping catches more. The Permission Request UI handles the rest gracefully. Not a phased replacement — a defense-in-depth stack.

**Permission UI modes:**
- **MainView:** Tap Allow/Deny buttons
- **Ghost FAB (in-game):** Card appears with buttons + mic auto-activates for voice approval ("yes"/"no")
- **Timeout:** 60s with no response → auto-deny. Agent narrates: "Team needed approval but you were busy."

## 6. CLAUDE.md Template Structure

### Layered Architecture

```
Claude Code Session Working Directory
├── CLAUDE.md (Core)              ← Gaimer-owned, auto-deployed, never user-edited
├── gaimer-overlays/
│   ├── streaming.md              ← User-selected or custom overlay
│   ├── obs-integration.md        ← Community template
│   └── custom.md                 ← User's own additions
```

### CLAUDE.md Core (owned by Gaimer)

Deployed by Gaimer during session setup. Defines the protocol contract.

```markdown
# Gaimer Team — Core Protocol

## Your Role
You are receiving tasks from Gaimer, a desktop gaming companion.
The user does NOT see your responses directly — a gaming agent
will narrate your findings in its own voice.

## Response Format
Return JSON matching the task_result schema:
- response: concise, voice-ready text (2-3 sentences for voice format)
- actions_taken: list of tools you used
- follow_up: optional next step suggestion
- artifacts: URLs, code, data worth saving

## Rules
1. Lead with the answer, not the process.
2. For "voice" format: 2-3 sentences max, written for speech.
3. For "detailed" format: full explanation, can be longer.
4. Use your tools freely — search, file ops, scripts, MCP servers.
5. If you can't complete a task, say so briefly.
6. Send status_update for tasks taking >10 seconds.
7. Never attempt to speak to the user directly or control Gaimer's UI.
8. Never modify files in Gaimer's application directory.

## Safety Boundaries
- File operations: user's home directory only, no system paths
- Network: web search and public APIs only, no authenticated services
  unless user has configured them via MCP servers
- Destructive actions (delete, overwrite): always request permission
  via permission_request message, never auto-execute
```

### Overlay Templates (user-selected, community-authored)

Overlays extend what Claude Code can do when handling Gaimer Team tasks. They don't modify the core protocol — they add domain knowledge and workflow patterns.

**Example: `streaming.md`**
```markdown
# Streaming Workflow

When the user asks about streaming, clips, or highlights:
- Replay segments are in ~/Library/replays/{session-id}/
- Use ffmpeg for video operations (installed via Homebrew)
- Output clips to ~/Movies/Gaimer Clips/

When asked to make a highlight reel:
1. Find recent replay segments
2. Identify key moments (kills, objectives, clutches)
3. Extract and concatenate with ffmpeg
4. Save to clips directory
5. Report back with file path and duration
```

**Example: `obs-integration.md`**
```markdown
# OBS Studio Integration

OBS WebSocket is available at localhost:4455
Password is in ~/.config/obs-studio/websocket.json

Available actions:
- Switch scenes: use obs-websocket MCP server
- Start/stop recording
- Set stream title
- Toggle sources

When the user says "start streaming" or "go live":
1. Switch to gaming scene
2. Start recording
3. Confirm via task_result
```

### Template Distribution

| Source | How it gets there |
|---|---|
| Gaimer-shipped defaults | Bundled in app, deployed on session setup |
| Community templates | Downloaded from Gaimer website, placed in overlays directory |
| User custom | User creates their own .md files in overlays directory |

### Overlay Management

- Gaimer's ConnectorCard settings show active overlays with toggles
- User can enable/disable overlays without deleting them
- Gaimer concatenates Core + active overlays when deploying to Claude Code session
- Core is always first, overlays appended in alphabetical order

### Progressive Context Enrichment

The CLAUDE.md Core can evolve over time to teach Claude Code more about Gaimer:

- **Phase 1:** Claude Code knows it's receiving tasks from a gaming companion, responds in voice-ready format
- **Phase 2:** Claude Code understands Gaimer's game packs, can reference them in file system
- **Phase 3:** Claude Code understands the capture pipeline, replay segments, can search/analyze them
- **Phase 4:** Claude Code understands the full Gaimer architecture — can suggest optimizations, debug issues, build custom workflows

Each phase is a CLAUDE.md Core revision — no protocol changes needed. The thin pipe stays thin.

## 7. Gaimer-Side Implementation

### IGaimerTeamService

```csharp
public interface IGaimerTeamService
{
    /// Submit a task to Gaimer Team. Fire-and-forget — returns task ID immediately.
    Task<string> SubmitTaskAsync(GaimerTeamTask task, CancellationToken ct = default);

    /// Cancel a pending task.
    Task CancelTaskAsync(string taskId, CancellationToken ct = default);

    /// Respond to a permission request from Claude Code.
    Task RespondToPermissionAsync(string permissionId, bool approved, CancellationToken ct = default);

    /// Fired when a task completes (success or error).
    event EventHandler<GaimerTeamResultEventArgs> TaskCompleted;

    /// Fired when a task sends a progress update.
    event EventHandler<GaimerTeamProgressEventArgs> TaskProgress;

    /// Fired when Claude Code requests permission for an action.
    event EventHandler<GaimerTeamPermissionEventArgs> PermissionRequested;

    /// Connection state.
    bool IsConnected { get; }
    bool IsConfigured { get; }

    /// Launch a new Claude Code session with channel plugin.
    Task<bool> LaunchSessionAsync(CancellationToken ct = default);

    /// Connect to an existing Claude Code session.
    Task<bool> ConnectExistingAsync(CancellationToken ct = default);

    /// Disconnect and optionally terminate owned session.
    Task DisconnectAsync(bool terminateOwnedSession = true);
}
```

### Models

```csharp
public record GaimerTeamTask
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public string Task { get; init; }
    public GaimerTeamContext Context { get; init; }
    public string ResponseFormat { get; init; } = "voice"; // "voice" | "detailed"
}

public record GaimerTeamContext
{
    public string Game { get; init; }
    public string Agent { get; init; }
    public string SessionId { get; init; }
    public string RecentActivity { get; init; }
    public string L1Context { get; init; }
    public string L2Context { get; init; }
}

public record GaimerTeamResult
{
    public string TaskId { get; init; }
    public string Status { get; init; }       // "complete" | "error"
    public string Response { get; init; }
    public List<string> ActionsTaken { get; init; }
    public string FollowUp { get; init; }
    public string ErrorCode { get; init; }
    public List<GaimerTeamArtifact> Artifacts { get; init; }
}

public record GaimerTeamArtifact
{
    public string Type { get; init; }  // "url" | "code" | "data"
    public string Title { get; init; }
    public string Content { get; init; }
}

public record GaimerTeamPermissionRequest
{
    public string Id { get; init; }
    public string TaskId { get; init; }
    public string Action { get; init; }
    public string Risk { get; init; }      // "low" | "medium" | "high"
    public int TimeoutSeconds { get; init; } = 60;
}

public class GaimerTeamResultEventArgs : EventArgs
{
    public GaimerTeamResult Result { get; init; }
}

public class GaimerTeamProgressEventArgs : EventArgs
{
    public string TaskId { get; init; }
    public string Message { get; init; }
}

public class GaimerTeamPermissionEventArgs : EventArgs
{
    public GaimerTeamPermissionRequest Request { get; init; }
}
```

### Gaming Agent Escalation (in BrainEventRouter or MainViewModel)

```csharp
// Gaming agent decides to escalate
var task = new GaimerTeamTask
{
    Task = userUtterance,
    Context = new GaimerTeamContext
    {
        Game = _sessionManager.CurrentGame,
        Agent = _sessionManager.ActiveAgent.Name,
        SessionId = _sessionManager.SessionId,
        RecentActivity = _contextEnvelope.L1Summary,
        L1Context = _contextEnvelope.L1Summary,
        L2Context = _contextEnvelope.L2Summary
    }
};

var taskId = await _gaimerTeam.SubmitTaskAsync(task);

// Agent tells user immediately
await _voicePipeline.SpeakAsync(
    "Let me hand that to the team. I'll let you know what they find.");
```

### Result & Permission Handlers (wired once at initialization)

```csharp
_gaimerTeam.TaskCompleted += (s, e) =>
{
    if (e.Result.Status == "complete")
    {
        var narration = $"The team's back. {e.Result.Response}";
        _ = _voicePipeline.SpeakAsync(narration);
        _ = _timelineFeed.AddGaimerTeamResult(e.Result);
    }
    else
    {
        _ = _voicePipeline.SpeakAsync(
            "The team ran into an issue with that one. We can try again later.");
    }
};

_gaimerTeam.PermissionRequested += (s, e) =>
{
    // Routes to Ghost Card (if in-game) or MainView alert
    _ = _permissionPresenter.ShowPermissionRequest(e.Request);
};
```

### DI Registration

```csharp
// MauiProgram.cs
services.AddSingleton<IGaimerTeamService, GaimerTeamService>();
```

### Relationship to Existing Services

```
IConversationProvider  → Voice chat (OpenAI Realtime)        — UNCHANGED
IBrainService          → Visual analysis (OpenRouter/Gemini)  — UNCHANGED
IGaimerTeamService     → Background tasks (Claude Code)       — NEW

All three are independent services in the DI container.
The gaming agent (not a router) decides when to escalate to Gaimer Team.
Brain-voice pipeline rules remain inviolate.
```

## 8. Channel Plugin Design

The channel plugin is the technical core — but intentionally minimal. ~200 lines of Node/Bun.

### Responsibilities

1. Listen on named pipe/Unix domain socket for Gaimer messages
2. Forward task messages to Claude Code as MCP notifications
3. Relay Claude Code responses back to Gaimer through the pipe
4. Forward permission requests from Claude Code to Gaimer
5. Health ping/pong

That's it. No validation, no state management, no retry logic. Dumb pipe.

### Plugin Structure (bundled in Gaimer repo)

```
src/gaimer-channel-plugin/
├── .claude-plugin/
│   └── plugin.json
├── package.json
├── src/
│   └── index.ts              ← Entry point (~200 lines)
├── .mcp.json                 ← Declares claude/channel capability
└── CLAUDE.md                 ← Core protocol file (deployed to session)
```

### plugin.json

```json
{
  "name": "gaimer-channel",
  "version": "1.0.0",
  "description": "Gaimer gaming companion channel for Claude Code",
  "author": {
    "name": "5DOF Studio"
  }
}
```

### Core Logic (pseudocode)

```typescript
import { McpServer } from "@modelcontextprotocol/sdk";

// 1. Declare channel capability
const server = new McpServer({
  capabilities: { "claude/channel": {} }
});

// 2. Create named pipe listener
const pipePath = platform === "darwin"
  ? `${homedir}/Library/Application Support/Gaimer/gaimer-team.sock`
  : "\\\\.\\pipe\\gaimer-team";

const pipeServer = createPipeServer(pipePath);

// 3. On message from Gaimer → forward to Claude Code
pipeServer.on("message", (msg) => {
  if (msg.type === "task_request") {
    server.sendNotification("gaimer/task", msg);
  }
  if (msg.type === "permission_response") {
    server.sendNotification("gaimer/permission_response", msg);
  }
  if (msg.type === "ping") {
    pipeServer.send({ type: "pong" });
  }
});

// 4. On response from Claude Code → relay to Gaimer
server.onNotification("gaimer/result", (msg) => {
  pipeServer.send(msg);
});

server.onNotification("gaimer/permission_request", (msg) => {
  pipeServer.send(msg);
});

// 5. Start
server.connect(process.stdin, process.stdout);  // MCP stdio to Claude Code
pipeServer.listen();                             // Named pipe to Gaimer
```

### How Gaimer Deploys It

**During LaunchSessionAsync:**
1. Gaimer locates the bundled plugin at `{app_resources}/gaimer-channel-plugin/`
2. Deploys CLAUDE.md Core + active overlays to a working directory
3. Launches Claude Code: `claude --channels {plugin_path} --dangerously-skip-permissions`
4. Waits for pipe to become available
5. Sends health ping, confirms connection

**During ConnectExistingAsync:**
1. Checks if pipe exists at known path
2. Connects, sends health ping
3. If responsive → connected. If not → "No active session."

## 9. Implementation Phases

### Phase A: Interface + Mock (No Claude Code Yet)

- Define `IGaimerTeamService`, models, event args
- Mock implementation returning canned responses after configurable delay
- Gaming agent escalation wiring — agent prompt updated with "Gaimer Team" concept
- Result handler — TaskCompleted → voice narration + timeline entry
- Permission handler — PermissionRequested → alert UI (simple Allow/Deny)
- Test full loop: user speaks → agent escalates → mock returns → agent narrates

**Validates:** The handoff pattern works end-to-end without any Claude Code dependency.

### Phase B: Channel Plugin + Connection

- Build the channel plugin (Node/Bun, ~200 lines)
- Named pipe / Unix domain socket IPC (macOS first, Windows later)
- `LaunchSessionAsync` — spawn Claude Code with channel plugin
- `ConnectExistingAsync` — discover and connect to running session
- Process health management (restart, reconnect, ping/pong)
- CLAUDE.md Core deployed to session
- Live test: real Claude Code responses through the channel

> **Resolve before this phase:** Q11 — Node vs Bun for channel plugin runtime

**Validates:** Gaimer can talk to Claude Code and get real results back.

### Phase C: Settings + ConnectorCard UX

- Team section in Settings — provider selection (Claude Code)
- Pre-flight checks (installation, authentication, browser OAuth flow)
- ConnectorCard for Claude Code — New Session / Connect Existing
- Connection state management in UI
- "Configure in Settings" alert guard on MainView

> **Resolve before this phase:** Q13 — Multiple concurrent sessions handling

**Validates:** User can set up and manage the Claude Code connection without touching a terminal.

### Phase D: Context Enrichment

- Wire SharedContextEnvelope into GaimerTeamContext
- L1/L2 context assembly from existing brain/session state
- Game-specific context (active game pack name, recent journal entries)
- Replay segment awareness (recent recordings listed in context)

**Validates:** Claude Code has enough context to give game-relevant answers.

### Phase E: CLAUDE.md Overlay System

- Overlay directory structure and file management
- Overlay toggle UI in ConnectorCard settings
- Template concatenation (Core + active overlays) on session deploy
- Ship 2-3 starter templates (general gaming, streaming/OBS, replay editing)
- Community template download flow

> **Resolve before this phase:** Q7 — Overlay size limits and token budget

**Validates:** Users can extend Claude Code's gaming capabilities without writing code.

### Phase F: Production Hardening

- Error handling, timeout management
- Task queue (multiple pending tasks)
- Connection recovery (pipe drops, Claude Code crashes)
- Telemetry (task latency, success rate, escalation frequency)
- Rate limiting (prevent rapid-fire voice escalation)
- Permission request timeout + auto-deny

> **Resolve before this phase:** Q12 — Session resume after crash (resubmit vs fail pending tasks)

**Validates:** Stable under real-world gaming session conditions.

### Phase G: Permission Request UI (Full)

- Ghost Card permission request display
- Voice approval (mic hot for "yes"/"no" when in-game)
- Permission history log
- Risk-based presentation (low/medium/high styling)

> **Resolve before this phase:** Q14 — Voice approval accuracy (existing pipeline vs keyword detection)

**Validates:** Users can approve Claude Code actions without leaving the game.

### Phase H: Windows Platform

- Named pipes implementation for Windows
- Claude Code installation/auth check for Windows
- Cross-platform testing
- Platform-specific socket paths and process management

**Validates:** Full feature parity on Windows.

## 10. Open Questions

### Resolved

| # | Original Question | Resolution |
|---|---|---|
| Q1 | Discord vs Telegram? | Neither — Claude Code Channels with custom plugin (D4) |
| Q2 | Bridge discovery? | Named pipe at known path. ConnectorCard UI for new/existing session (D13) |
| Q3 | Multi-turn tasks? | Deferred beyond V1. Each task is independent. |
| Q4 | Status updates UX? | Gaming agent optionally relays progress. Ghost card can show "Team working..." |
| Q5 | Offline fallback? | Agent says "Team's not available right now." No fallback transport in V1. |
| Q6 | Rate limiting? | Phase F. Prevent rapid-fire voice escalation. |
| Q8 | Pack conflicts? | N/A — CLAUDE.md overlays don't conflict the way tool packs did. Concatenation order is alphabetical. |
| Q9 | Pack monetization? | N/A — overlays are markdown files. Community shares freely. |
| Q10 | NanoClaw? | Unnecessary — Channels architecture eliminates the need for middleware. |

### Still Open (resolve at phase gate)

| # | Question | Context | Resolve Before |
|---|---|---|---|
| Q7 | **Overlay size limits** | Concatenated CLAUDE.md (core + overlays) could get large. What's the practical token limit before Claude Code's context is impacted? | Phase E |
| Q11 | **Channel plugin runtime** | Node vs Bun. Bun is recommended by Anthropic for channels, but adds a dependency. Node is more universal. | Phase B |
| Q12 | **Session resume after crash** | If Claude Code crashes mid-task, does Gaimer resubmit pending tasks on reconnect, or treat them as failed? | Phase F |
| Q13 | **Multiple concurrent sessions** | Can a user run multiple Claude Code sessions? How does Gaimer avoid interfering with a non-Gaimer session? | Phase C |
| Q14 | **Voice approval accuracy** | "Yes"/"no" voice recognition for permission requests. Use existing voice pipeline or simple keyword detection? | Phase G |

## 11. Deferred (Beyond V1)

- **Codex provider** — alternative to Claude Code behind the same IGaimerTeamService interface
- **Multi-turn task conversations** — Claude Code maintains conversation state across related tasks
- **Skill Pack marketplace** — if CLAUDE.md overlays prove insufficient, revisit structured tool bundles
- **Cross-session task persistence** — resume pending tasks after app restart
- **Gaimer Team Skill Packs** — player-created tool bundles for Claude (original spec Sections 8.1-8.9). Deferred in favor of simpler CLAUDE.md overlay system. Revisit if overlays prove insufficient for complex tool integration.

---

*Previous version of this spec (2026-04-01) used Discord/Telegram messaging bridges as primary transport. Revised 2026-04-06 to use Claude Code Channels after research into Claude Code's remote access ecosystem revealed Channels as the native, event-driven integration path. See `WITNESS/gaimer_spec_docs/GAIMER_TEAM_ARCHITECTURE_DECISIONS.md` for the full decision narrative.*
