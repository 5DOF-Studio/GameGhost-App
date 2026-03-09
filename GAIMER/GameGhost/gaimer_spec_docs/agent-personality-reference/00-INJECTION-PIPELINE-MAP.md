# Agent Personality Injection Pipeline — OpenClaw vs gAImer

## How OpenClaw Injects Personality

OpenClaw composes the system prompt from **4 layers** at session start:

```
Layer 1: Base Prompt (core safety + instructions)     — hardcoded
Layer 2: Skills (tool descriptions as compact XML)     — from skills/ directory
Layer 3: Bootstrap Files (SOUL + AGENTS + USER + etc)  — from workspace/ directory
Layer 4: Per-Run Overrides (additional context)         — dynamic
```

All bootstrap files are injected into the system prompt on **every turn**.
Individual files capped at 20,000 chars. Total bootstrap capped at 150,000 chars.

The system prompt builder (`buildAgentSystemPrompt()`) concatenates:
1. Tooling section
2. Safety guardrails
3. Skills list
4. Workspace files (AGENTS.md, SOUL.md, IDENTITY.md, USER.md, TOOLS.md, etc.)
5. Sandbox info
6. Current date/time
7. Reply formatting rules

---

## How gAImer Currently Injects Personality

gAImer has **ONE injection point** — `Agent.SystemInstruction`:

```
Agent.SystemInstruction → Voice Provider system prompt
```

### Gemini Path (GeminiLiveService.cs:162-168)
```csharp
system_instruction = new
{
    parts = new[]
    {
        new { text = agent.SystemInstruction }   // <-- SINGLE STRING
    }
}
```

### OpenAI Path (OpenAIRealtimeService.cs:166)
```csharp
session = new
{
    instructions = agent.SystemInstruction,      // <-- SINGLE STRING
    voice = _voice,
    ...
}
```

### Runtime Context Injection (BrainEventRouter.cs)
Brain analysis flows to voice as text via `SendContextualUpdateAsync()`:
```csharp
// These arrive as conversation messages DURING the session
_voiceAgent.SendContextualUpdateAsync(FormatForVoice(hint))        // normal
_voiceAgent.SendContextualUpdateAsync($"[URGENT] {result.VoiceNarration}")  // urgent
```

---

## The Gap

| OpenClaw Has | gAImer Has | Gap |
|---|---|---|
| SOUL.md (philosophy/personality) | 5-line BEHAVIOR block | No depth — "analytical precision with enthusiasm" is too vague |
| STYLE.md (voice character) | ChessToolGuidance VOICE OUTPUT section | Only covers chess notation translation, not personality voice |
| AGENTS.md (operating contract) | Embedded in SystemInstruction | Workflow, priorities, quality bar are missing |
| SKILL.md (situational modes) | None | No opening/midgame/endgame/blunder modes |
| IDENTITY.md (metadata) | Agent class properties | Already good — Name, Id, IconImage, etc. |
| USER.md (user context) | None | No skill-level detection or user preference memory |
| Anti-patterns ("never do") | None | Critical for voice — need "never say" list |
| Good/bad examples | None | No calibration material |

---

## Proposed Architecture for gAImer

Since gAImer injects through a **single SystemInstruction string**, we compose the
personality from separate design files into one concatenated prompt at compile time.

### Design Files (human-readable, in spec docs)
```
GAIMER/GameGhost/gaimer_spec_docs/agents/
  leroy/
    SOUL.md           — Who Leroy IS (personality, worldview, opinions)
    STYLE.md          — How Leroy TALKS (voice character, vocabulary, reactions)
    BEHAVIOR.md       — How Leroy ACTS (operating contract, workflow, tool usage)
    SITUATIONS.md     — Context-specific modes (opening, midgame, endgame, blunder, etc.)
    ANTI-PATTERNS.md  — What Leroy NEVER does (never-say list, voice failures)
    EXAMPLES.md       — Good outputs + bad outputs for calibration
  wasp/
    SOUL.md
    STYLE.md
    BEHAVIOR.md
    SITUATIONS.md
    ANTI-PATTERNS.md
    EXAMPLES.md
  derek/
    (same structure)
```

### Code Integration (Agent.cs)
The design files get **composed** into `SystemInstruction` as structured sections:

```csharp
public static Agent Chess { get; } = new()
{
    SystemInstruction = $"""
        {LeroySoul}           // WHO YOU ARE — from SOUL.md
        {LeroyStyle}          // HOW YOU TALK — from STYLE.md
        {LeroyBehavior}       // HOW YOU ACT — from BEHAVIOR.md
        {LeroySituations}     // SITUATIONAL MODES — from SITUATIONS.md
        {LeroyAntiPatterns}   // NEVER DO — from ANTI-PATTERNS.md
        {ChessToolGuidance}   // TOOLS — already exists
        """
};
```

Each section is a `const string` in Agent.cs (like `ChessToolGuidance` already is).

### Runtime Context Layer (already exists)
```
Session Start:  SystemInstruction loaded into voice provider
During Game:    BrainEventRouter sends context updates as conversation messages
                - [CHESS BRAIN HINT] with position analysis
                - [URGENT] for critical moments
                - VoiceNarration for routine updates
```

### What We DON'T Need (non-autonomous agent)
- HEARTBEAT.md — no periodic check-ins (agent is per-session)
- BOOT.md — no startup ritual (agent starts when user selects it)
- BOOTSTRAP.md — no first-run discovery (agent personality is predefined)
- MEMORY.md daily logs — no cross-session memory (yet — Phase 07 Persistence)
- USER.md as runtime file — could be future feature (skill level detection)

---

## Token Budget

Voice providers have system prompt limits. Budget allocation:

| Section | Target Tokens | Purpose |
|---|---|---|
| SOUL (personality) | ~200 | Core identity, worldview |
| STYLE (voice) | ~150 | Speech patterns, vocabulary, reactions |
| BEHAVIOR (contract) | ~200 | Workflow, priorities, tool usage |
| SITUATIONS (modes) | ~300 | Game-phase-specific behavior |
| ANTI-PATTERNS | ~100 | Never-do list |
| ChessToolGuidance | ~350 | Already exists, proven |
| **Total** | **~1300** | Well within 4K system prompt budget |

The existing Dross system prompt (system-prompt.md) is ~2500 tokens and works well,
so 1300 tokens for the structured personality is conservative and safe.

---

## Next Steps

1. Use BUILD template (08-BUILD) to design Leroy's personality
2. Fill in SOUL, STYLE, BEHAVIOR, SITUATIONS, ANTI-PATTERNS for Leroy
3. Create good-outputs/bad-outputs examples
4. Compose into SystemInstruction constants
5. Test with live voice session
6. Repeat for Wasp (different personality, same structure)
