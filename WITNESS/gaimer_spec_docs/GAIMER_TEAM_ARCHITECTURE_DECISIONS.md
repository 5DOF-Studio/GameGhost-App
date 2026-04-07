# Gaimer Team Architecture Decisions — Build-in-Public Research Preview

**Date:** 2026-04-06
**Author:** Ike Nlemadim, 5DOF Studio
**Context:** Gaimer Desktop v2 — AI Gaming Companion

---

## The Problem

Gaimer's gaming agents (Leroy, Wasp, RASA) are powerful within their domain — they can analyze your screen, comment on your gameplay, and use tools like Stockfish for chess analysis. But when a user asks something beyond the game context — "what's the meta build right now?", "make a highlight reel from that last match", "go live on Twitch" — the agent hits a wall. It doesn't have web search, file system access, or the ability to control other software.

We needed a way for the gaming agent to delegate these "computer tasks" to something that does.

## The Research

We started by mapping Claude Code's entire remote access ecosystem. The landscape has matured rapidly in early 2026:

- **Remote Control** (Feb 2026) — encrypted bridge from claude.ai/code or mobile apps to a local Claude Code session. Great for humans steering work, but not for programmatic integration.
- **Channels** (Mar 2026) — MCP servers that push events from external platforms (Telegram, Discord, iMessage) into running Claude Code sessions. Event-driven, ambient, async.
- **Agent SDK** — `@anthropic-ai/claude-agent-sdk` on npm. Direct programmatic access via `query()` async generator. Full control, stateless per call.
- **Headless CLI** (`claude -p`) — non-interactive mode for scripting and CI/CD.
- **`claude mcp serve`** — exposes Claude Code's built-in tools via MCP stdio. Local only, no network.

Plus a thriving community ecosystem of Discord bots, Telegram bridges, web UIs, and REST API wrappers — all built on top of the Agent SDK or headless CLI.

## The Decision: Channels Over Agent SDK

This was the pivotal choice. Both could work. Here's why we chose Channels.

### The Agent SDK Approach

The Agent SDK makes Gaimer the orchestrator. You call `query()` with a prompt and MCP tools, and Claude processes the request. Clean, controlled, testable.

The problem: **every tool Claude can use, you must explicitly expose.** If the user asks Gaimer to make a highlight reel, Gaimer would need to provide ffmpeg access, file system tools, and multi-step reasoning capabilities through the SDK. If they ask Gaimer to start their Twitch stream, Gaimer needs an OBS integration. Every new capability requires new code on Gaimer's side.

You're essentially rebuilding Claude Code's tool set inside your .NET app.

### The Channels Approach

Channels flip the model. Gaimer doesn't orchestrate — it **sends intents and receives results.** Claude Code already has file ops, bash, web search, MCP servers, and whatever plugins the user has configured. Gaimer doesn't need to re-provide any of it.

"Make a highlight reel" goes through the channel → Claude Code uses ffmpeg, accesses the file system, does multi-step reasoning → result comes back. Gaimer never needed to know ffmpeg exists.

The key insight that locked in the decision: **the user's machine is the computer. Their Claude Code config is their tool ecosystem. Gaimer is just the voice trigger and gaming-context layer.**

### What Tipped It

1. **Capability scope.** With the SDK, you build every integration. With Channels, you get everything Claude Code has for free — and everything the user adds later.

2. **Community extensibility.** Users extend Gaimer's capabilities by writing CLAUDE.md templates, not code. A Twitch streamer adds their OBS config to a markdown file and suddenly their gaming agent can control their stream. No plugin development required.

3. **Future-proofing.** When Anthropic adds features to Claude Code, Gaimer automatically benefits through the channel. With the SDK, each new capability requires integration work.

4. **Staying thin.** The gaming agents are already complex (voice pipeline, brain analysis, screen capture, replay recording). Adding Claude orchestration would bloat the codebase. Let Claude Code be Claude Code.

### The Trade-offs We Accepted

- **Session persistence.** Claude Code must remain running. If it closes, messages are lost. We handle this by having Gaimer manage Claude Code as a background process — launch, monitor, restart on crash. The user never opens a terminal.

- **`--dangerously-skip-permissions`.** Required for autonomous operation. Sounds scary, but the user explicitly opts in during setup, and we scope behavior through CLAUDE.md safety rules. Plus we designed a permission request UI for actions that still need approval — three layers of defense, not one.

- **Less control.** Claude Code has full access to its tools. We mitigate with CLAUDE.md safety boundaries (home directory only, no system paths, destructive actions require permission).

- **Research preview status.** Channels launched March 2026. The API may evolve. But Anthropic shipped three official channel plugins and it's clearly a strategic feature. We're building on the grain of the platform.

## The Architecture We Landed On

### Thin Pipe

The channel plugin is intentionally minimal — ~200 lines of Node/Bun. It's a dumb pipe: listen on a named pipe for Gaimer messages, forward them to Claude Code as MCP notifications, relay responses back. No validation, no state management, no business logic.

All intelligence lives in two places:
- **Gaimer's gaming agent prompts** — decide when to escalate vs handle locally
- **CLAUDE.md** — teaches Claude Code how to respond to gaming tasks

### Layered CLAUDE.md

We split CLAUDE.md into two layers:
- **Core** (owned by Gaimer, never user-edited) — protocol rules, safety boundaries, response format
- **Overlays** (user-selected, community-authored) — domain workflows like streaming, OBS integration, replay editing

This means the protocol is always correct, and the community can extend capabilities without touching core infrastructure.

### Named Pipes, Not WebSocket

For the IPC between Gaimer (.NET MAUI) and the channel plugin (Node/Bun), we chose Unix domain sockets on macOS and named pipes on Windows over WebSocket. Reasons:
- Zero network exposure — nothing shows up in netstat
- File-system permissions control access
- No port allocation or discovery needed — fixed, known socket path
- Lower latency than TCP
- Gaimer already does platform-specific code (native xcframeworks)

### Agent-Owned Escalation

No intent classifier or router. The gaming agent itself decides when a request should leave the game context. "What's the best move?" stays local (Stockfish). "What's the meta build right now?" goes to the team (web research). The agent knows its own limits.

The user can also explicitly invoke: "Have the team look into this."

### Permission Request Defense-in-Depth

Three layers coexist:
1. `--dangerously-skip-permissions` blocks most prompts
2. CLAUDE.md safety scoping catches more via rules
3. Permission Request UI handles whatever gets through — buttons in MainView, voice approval ("yes"/"no") via Ghost FAB when in-game

This isn't a phased rollout — all three layers are active simultaneously.

## Why This Matters

Nobody is using Claude Code Channels as a gaming interface layer. The feature is barely weeks old. We're creating a new category: a voice-activated gaming companion that turns Claude Code into the user's ambient AI computer.

The user talks to their agent. The agent has a team. The team is Claude Code with full access to the user's machine. And the community extends it by writing markdown files.

That's the architecture. Here's how we're building it.

## Implementation Roadmap

| Phase | What | Validates |
|-------|------|-----------|
| A | Interface + Mock | Handoff pattern works E2E without Claude Code |
| B | Channel Plugin + Connection | Gaimer talks to real Claude Code |
| C | Settings + ConnectorCard UX | User manages connection without a terminal |
| D | Context Enrichment | Claude Code gets game-relevant context |
| E | CLAUDE.md Overlay System | Community extensibility works |
| F | Production Hardening | Stable under real gaming sessions |
| G | Permission Request UI | In-game approval via voice or buttons |
| H | Windows Platform | Full cross-platform parity |

## Open Questions (Deferred to Phase Gates)

We identified 5 open questions during the design process. Rather than force premature decisions, each is tagged to the phase where it becomes relevant:

- **Q7:** How large can concatenated CLAUDE.md get before context impact? → Phase E
- **Q11:** Node vs Bun for the channel plugin? → Phase B
- **Q12:** Resubmit or fail pending tasks after crash? → Phase F
- **Q13:** How to handle multiple concurrent Claude Code sessions? → Phase C
- **Q14:** Voice approval via existing pipeline or keyword detection? → Phase G

## What We Considered and Rejected

| Option | Why Rejected |
|--------|-------------|
| Discord/Telegram messaging bridges | Channels are native, event-driven, and purpose-built. Third-party bridges add latency and fragility. |
| Agent SDK as primary transport | Makes Gaimer the orchestrator. Every Claude Code capability must be re-exposed. Doesn't scale. |
| NanoClaw middleware | Channels eliminate the need for middleware between Gaimer and Claude Code. |
| WebSocket IPC | Named pipes have no network exposure, no port allocation, and lower latency. WebSocket buys nothing for local IPC. |
| Smart channel plugin (schema validation, state management) | Complexity that fights the thin-pipe thesis. Claude Code and Gaimer can each handle their own concerns. |
| Intent classifier / router | Gaming agents already know their limits. Adding a classifier adds latency and a new failure mode for no gain. |
| Custom GaimerProtocol header | The named pipe IS the protocol boundary. Headers are overhead with no consumer. |

---

*Full technical spec: `WITNESS/gaimer_spec_docs/CLAUDE_AGENT_INTEGRATION_SPEC.md`*
*Project: github.com/5DOF-Studio/Gaimer-app (gaimer-v2 branch)*
