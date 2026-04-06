# Chess Agent Implementation Gap Analysis

**Status:** Review
**Date:** March 16, 2026
**Scope:** Current Leroy/Wasp chess-agent implementation reviewed against intended outcomes, current product direction, and reusable lessons for future agents such as `Rasa`.

## Executive Summary

The current chess agents are strong on personality structure and voice consistency, but the surrounding implementation is still heavily hardcoded around chess and contains several mismatches between the intended behavior and the actual behavior.

The main pattern to avoid copying forward is this:

- personality is modular
- capabilities are not modular

That is why the current chess work succeeds as a specialist vertical but fails as a reusable foundation for a game-agnostic commentator agent.

The most user-visible defect in the current chess experience is not the personality system. It is perception consistency. The agent is intended to behave like it can see the live board, but some user-facing paths still claim it cannot.

## Intent vs Reality

### Intended outcome

The chess agents appear intended to:

- watch the board in near real-time
- reason about what they see
- use authoritative tools when needed
- maintain consistent personality across voice, brain, and chat
- track game progress over time
- teach and comment without hallucinating

### Actual reality

They currently:

- achieve strong voice/personality composition
- inject a useful compact brain prefix
- have a real authoritative chess engine path
- have inconsistent board-visibility behavior across voice, brain, and chat
- still depend on chess-only prompt assumptions throughout the brain pipeline
- expose tools session-wide instead of agent-wide
- use an intentionally non-browsing knowledge tool under a misleading `web_search` name
- keep only session-local, chess-shaped memory

## What Is Working Well

### 1. Personality composition architecture is solid

The `SOUL` / `STYLE` / `BEHAVIOR` / `SITUATIONS` / `ANTI-PATTERNS` split is the strongest part of the current design.

Why it works:

- separates identity from operational policy
- makes distinct voices easier to maintain
- supports compact brain prompts and richer voice prompts
- aligns well with OpenClaw-style personality authoring

Relevant code:

- [Agent.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Models/Agent.cs#L63)
- [GeminiLiveService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/GeminiLiveService.cs#L143)
- [OpenAIRealtimeService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/OpenAIRealtimeService.cs#L157)

Recommendation:

- Preserve this pattern for every future agent.

### 2. Brain personality prefix is a good compromise

The compact brain prefix avoids dumping the entire speaking persona into the analytical pipeline.

Why it works:

- keeps the brain focused
- preserves identity
- reduces prompt bloat

Recommendation:

- keep compact brain prefixes, but make them domain-specific rather than chess-global.

### 3. Stockfish path is the clearest grounding mechanism in the system

The `analyze_position_engine` path is a real grounding tool with input validation and structured output.

Relevant code:

- [ToolExecutor.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/Brain/ToolExecutor.cs#L174)

Recommendation:

- future agents need comparable grounding tools in their own domain, not just LLM-only reasoning.

## Implementation Gaps

### Gap 1. Board-visibility contract is inconsistent across voice, brain, and chat

Severity: High

Problem:

The chess agent is intended to behave as if it can see the live board while connected to a game. In practice, when asked about board state, the voice agent can still say that it cannot see the board.

Likely cause:

- The architecture correctly routes visual understanding through the brain rather than sending raw images to voice.
- But not every user-facing prompt path is consistently framed around delegated live perception.
- Some fallback or direct chat paths likely leak generic assistant language instead of the connected chess-agent contract.

Relevant areas:

- [Agent.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Models/Agent.cs#L464)
- [OpenAIRealtimeService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/OpenAIRealtimeService.cs#L157)
- [GeminiLiveService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/GeminiLiveService.cs#L143)
- [ChatPromptBuilder.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/ChatPromptBuilder.cs#L23)

Impact:

- Breaks immersion immediately
- Contradicts the agent's product promise
- Makes the system feel less capable than it is
- Undermines trust in all subsequent commentary

Recommendation:

- Make "speak as a live board observer while connected" a hard behavioral contract across all user-facing paths.
- Audit voice session setup, text chat prompts, and fallback prompts for phrases that imply blindness.
- Ensure the agent only admits lack of board visibility when the system explicitly knows the board context is stale or unavailable.
- Add tests asserting that a connected chess agent does not say "I can't see the board", "I need a screenshot", or equivalent generic vision disclaimers.

### Gap 2. Brain prompt builder is fully chess-hardcoded

Severity: High

Problem:

The main brain prompt builder hardcodes chess board assumptions, FEN extraction, Stockfish usage, and chess-only connection guidance.

Relevant code:

- [BrainPromptBuilder.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/BrainPromptBuilder.cs#L29)

Impact:

- Blocks reuse for any non-chess agent
- Encourages bad abstractions where “agent-specific” logic is actually global
- Makes a new agent inherit false assumptions by default

Recommendation:

- Replace the single prompt builder with agent- or domain-specific prompt builders.
- Keep a shared framework for context assembly, but move capabilities and output contracts into per-agent modules.

### Gap 3. Tool gating is session-wide, not agent-wide

Severity: High

Problem:

All in-game sessions currently get chess tools, regardless of active agent.

Relevant code:

- [SessionManager.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/SessionManager.cs#L36)

Impact:

- Non-chess agents would see chess tools
- Tool choice becomes misleading and unstable
- Agent capability boundaries are not enforceable

Recommendation:

- Move tool selection to agent capabilities.
- `SessionManager` should expose state.
- Active agent or an agent capability registry should decide available tools.

### Gap 4. ToolDefinition catalog is global and mixes generic with chess-specific tools

Severity: High

Problem:

The tool catalog combines universal and chess-specific tools in one static global list.

Relevant code:

- [ToolDefinition.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Models/ToolDefinition.cs#L14)

Impact:

- Prevents clean specialization
- Encourages accidental leakage of domain tools
- Makes UI and policy harder to reason about

Recommendation:

- Split tools into:
  - global tools
  - agent-scoped tools
  - optionally domain-scoped tool packs

### Gap 5. `web_search` naming and contract do not match its intentional behavior

Severity: High

Problem:

The current tool behavior is intentional for chess: it performs a lightweight LLM knowledge lookup rather than real live browsing. The gap is that the tool is named `web_search`, which implies external retrieval and provenance it does not provide.

Relevant code:

- [ToolDefinition.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Models/ToolDefinition.cs#L39)
- [ToolExecutor.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/Brain/ToolExecutor.cs#L422)

Impact:

- Misleads prompts and future implementers
- Creates the impression of source-backed retrieval when none exists
- Becomes a bad template for future agents that really do need browsing or provenance

Recommendation:

- If the current behavior is the desired chess behavior, rename it to something like `knowledge_lookup` or `knowledge_lookup_llm`.
- If keeping the `web_search` name for compatibility, explicitly document that it is non-browsing prior-knowledge synthesis.
- For future agents like `Rasa`, add a separate real search-backed tool rather than overloading this one.

### Gap 6. Strategic analysis tool lacks grounding input

Severity: Medium

Problem:

`analyze_position_strategic` asks the LLM to analyze “the current chess position” without passing image data, FEN, or a structured board representation.

Relevant code:

- [ToolExecutor.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/Brain/ToolExecutor.cs#L301)

Impact:

- Hallucination risk
- Reduces trust in the explanatory layer
- Weakens the intended engine-plus-explanation design

Recommendation:

- Pass a grounded representation into the strategic tool.
- Minimum acceptable input: FEN plus player color and focus.
- Better input: FEN plus prior engine output plus current board observations.

### Gap 7. The journal is chess-shaped and session-only

Severity: Medium

Problem:

The game journal stores move-number/FEN-centric entries in memory only.

Relevant code:

- [GameJournalService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/GameJournalService.cs#L5)

Impact:

- Good for chess session replay
- Not suitable as a reusable knowledge layer
- Not durable across sessions
- Does not support broader commentary or player modeling

Recommendation:

- Keep the chess journal for chess if useful.
- Introduce a separate generic session knowledge store for future agents.
- Do not market it as durable memory until persistence exists.

### Gap 8. Chat prompt builder still assumes chess in generic text flows

Severity: Medium

Problem:

Text chat behavior hardcodes chess notation rules and chess tool advice.

Relevant code:

- [ChatPromptBuilder.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/ChatPromptBuilder.cs#L42)
- [ChatPromptBuilder.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Services/ChatPromptBuilder.cs#L91)

Impact:

- Makes “generic” chat actually chess-biased
- Will confuse non-chess agents
- Blurs medium rules with domain rules

Recommendation:

- Split text-medium rules from domain rules.
- Let the active agent inject domain-specific chat guidance.

### Gap 9. Agent metadata suggests specialization, but runtime capability enforcement is weak

Severity: Medium

Problem:

Agents expose `Tools`, `Type`, `CaptureConfig`, and personality blocks, but runtime decisions still rely on shared global logic instead of those agent properties.

Relevant code:

- [Agent.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Models/Agent.cs#L49)
- [Agent.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop/Models/Agent.cs#L464)

Impact:

- Agent definitions look richer than they functionally are
- Future contributors may think capabilities are modular when they are not

Recommendation:

- Make agent metadata authoritative.
- Runtime tool exposure, prompt builder selection, and capture policy should derive from the active agent definition.

### Gap 10. Current tests protect chess behavior but not future extensibility

Severity: Medium

Problem:

Many integration tests assert that chess agents have consistent chess tools and behavior. That is useful, but there are few guardrails ensuring non-chess agents remain free of chess-only assumptions.

Relevant code:

- [PipelineIntegrationTests.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/src/WitnessDesktop/WitnessDesktop.Tests/Integration/PipelineIntegrationTests.cs#L222)

Impact:

- Regressions toward chess-global assumptions are easy to introduce
- Refactoring toward a general commentator agent is riskier than it needs to be

Recommendation:

- Add tests for:
  - agent-scoped tool availability
  - prompt builder selection by agent
  - non-chess agents receiving no chess-only instructions
  - intentional non-browsing knowledge tool responses for chess
  - source-backed search tool responses for future browsing-enabled agents

### Gap 11. Product direction and implementation are currently misaligned

Severity: High

Problem:

Current product guidance says the near-term wedge is a single game-agnostic commentator with minimal tools and careful claims, but the codebase is still optimized around specialist chess coaching.

Relevant docs:

- [AGENT_HANDOFF_INSTRUCTIONS.md](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/WITNESS/AGENT_HANDOFF_INSTRUCTIONS.md#L74)
- [HANDOFF.md](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/chronicles/HANDOFF.md#L91)

Impact:

- New work can pull in the wrong abstractions
- UI and architecture may overfit to chess specialist workflows

Recommendation:

- Treat the chess agent as a successful vertical slice, not the universal blueprint.
- Extract only the reusable pieces: personality composition, compact brain prefix, capture configuration, and real grounding-tool patterns.

## Recommendations by Outcome

### If the goal is to improve Leroy/Wasp as chess agents

Do this:

1. Ground `analyze_position_strategic` with FEN and prior engine output
2. Make `get_game_state` return richer chess state if promised by docs
3. Improve journal summaries beyond opening FEN and latest description
4. Add explicit uncertainty handling around unreadable boards and partial position reads
5. Make the fake `web_search` naming honest

### If the goal is to create Rasa without breaking current chess behavior

Do this:

1. Add agent-scoped capability registry
2. Add a game-agnostic prompt builder path without touching chess prompts first
3. Introduce a generic session knowledge store
4. Add real search-backed research tools with provenance
5. Keep current chess agent runtime intact behind existing code paths until Rasa is validated

## Suggested Refactor Order

### Step 1. Capability separation

- Introduce agent capability resolution
- Stop using `SessionManager` as the final tool authority

### Step 2. Prompt separation

- Extract chess prompt builder
- Create generic commentator prompt builder

### Step 3. Memory separation

- Keep `GameJournalService` for chess
- Add generic session knowledge store for future agents

### Step 4. Search integrity

- Replace fake `web_search`
- Add source-aware result contracts

### Step 5. Tests

- Add non-chess negative tests
- Add source-provenance assertions
- Add agent-capability tests

## Acceptance Criteria for Gap Closure

The chess agent implementation can be considered structurally healthy when:

- chess-specific behavior lives in chess-specific modules
- non-chess agents do not inherit chess tools or prompts
- research tools reflect what they actually do
- explanatory analysis is grounded in real state
- session memory and durable knowledge are clearly separated

## Bottom Line

The chess agents are not a bad implementation. They are a good specialist implementation sitting on top of abstractions that are not yet generalized.

That means the right move is not to throw away the chess work. The right move is to isolate what is genuinely reusable and stop treating chess-specific assumptions as system-wide defaults.
