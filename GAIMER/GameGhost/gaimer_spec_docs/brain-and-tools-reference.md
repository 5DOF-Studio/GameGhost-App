# Dross Brain & Tools Reference

**Purpose:** Complete technical reference for replicating the Dross chess voice agent on the main Gaimer app. Covers the Brain analysis pipeline, prompt injections pushed to the voice agent, and the exact JSON output schemas for all 5 client tools.

**Last updated:** 2026-02-24
**Source repo:** `gaimer-build-in-public-website`

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Brain System Prompts (LLM Edge Function)](#2-brain-system-prompts-llm-edge-function)
3. [Prompt Injections (Contextual Updates)](#3-prompt-injections-contextual-updates)
4. [Tool Output Schemas](#4-tool-output-schemas)
5. [Brain Type Definitions](#5-brain-type-definitions)
6. [Interest Detection Thresholds](#6-interest-detection-thresholds)

---

## 1. Architecture Overview

```
User Move / AI Move
       |
       v
ChessBrainService.onMoveCompleted()
       |
       v
Web Worker (minimax, depth 4)
       |
       v
handleWorkerMessage()
       |
       +--> detectInterest() --> BrainHint --> onHint callback
       |                                         |
       |                                         v
       |                              sendContextualUpdate()
       |                              (pushed to ElevenLabs agent)
       |
       +--> [if high urgency] requestLLMCommentary()
       |                         |
       |                         v
       |                   Supabase Edge Function
       |                   (chess-brain-analysis)
       |                         |
       |                         v
       |                   GPT-4o-mini commentary
       |                         |
       |                         v
       |                   Updated BrainHint pushed to agent
       |
       v
analysisCache updated
(tools read from cache instantly)
```

**Key principle:** The Brain analyzes proactively after every move. Tool calls from the voice agent are instant cache reads (0-1ms), not async requests.

---

## 2. Brain System Prompts (LLM Edge Function)

**Edge function:** `supabase/functions/chess-brain-analysis/index.ts`
**Model:** GPT-4o-mini
**Two request types:** `commentary` and `opening_suggestions`

### 2a. Commentary Prompt

Triggered automatically when Brain detects a high-urgency position (eval swing > 200cp).

**System prompt:**
```
You are a concise chess commentator. Analyze the position and provide brief, engaging commentary.
Keep responses to 1-2 sentences. Be specific about the position. Use natural, conversational language.
Do not use chess notation in your response -- speak as if explaining to a casual player.
```

**User prompt template:**
```
Current position (FEN): {fen}
Recent moves: {last 5 moves as SAN, comma-separated}
What happened: {signal description from lookup table}
Evaluation: {evaluation}cp ({evalDelta}cp change)
Best response: {bestMove in UCI notation}
```

**Signal description lookup:**
| Signal | Description |
|--------|-------------|
| `danger` | Check or threat detected |
| `opportunity` | Strong move available |
| `blunder` | A mistake was made |
| `brilliant` | An excellent move was played |

**LLM parameters:**
- `max_tokens`: 150
- `temperature`: 0.7

**Request payload shape:**
```json
{
  "type": "commentary",
  "fen": "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
  "moveHistory": ["e2e4", "e7e5", "g1f3"],
  "interestSignal": "opportunity",
  "evaluation": 150,
  "evalDelta": 120,
  "bestMove": "g8f6"
}
```

**Response shape:**
```json
{
  "success": true,
  "commentary": "White's knight is putting real pressure on the center. Black needs to be careful about that pawn push."
}
```

### 2b. Opening Suggestions Prompt

Triggered once when the Brain initializes (before game starts).

**System prompt:**
```
You are a friendly chess coach. Suggest 3 opening strategies for a beginner-intermediate player.
For each opening, provide a creative name, the first 2-3 moves in algebraic notation, and a one-sentence description.
Keep it encouraging and fun.
```

**User prompt:**
```
Suggest 3 chess opening strategies. Return as JSON array: [{ name, moves: ['e4', 'Nf3', ...], description }]
```

**LLM parameters:**
- `max_tokens`: 300
- `temperature`: 0.8

**Response shape:**
```json
{
  "success": true,
  "openings": [
    {
      "name": "The Italian Stallion",
      "moves": ["e4", "Nf3", "Bc4"],
      "description": "A classic aggressive opening that fights for the center and develops your pieces quickly."
    }
  ]
}
```

---

## 3. Prompt Injections (Contextual Updates)

These are messages pushed to the ElevenLabs agent via `conversation.sendContextualUpdate(text)`. The agent receives them as system-level context, not as user messages. They guide agent behavior without the user hearing them.

### 3a. Game Start

**Trigger:** Agent status transitions to `connected` (500ms delay to ensure WebSocket is established).
**Fires once per session.**

**Format:**
```
[GAME START] Board: starting position. White (player) to move first. Player name: {userName}. Session active.
```

If no username is available, the `Player name:` segment is omitted.

**Source:** `useChessVoiceAgent.ts` lines 235-248

### 3b. Brain Hints

**Trigger:** Brain's `detectInterest()` returns a non-null hint after analyzing a move.
**Fires on every interesting position (eval swing > 100cp).**

**Format:**
```
[BRAIN SIGNAL] {signal} | {urgency} | Eval: {evaluation}cp | Delta: {evalDelta}cp | Suggested: {suggestedMove}
```

**Examples:**
```
[BRAIN SIGNAL] opening | medium | Eval: 30cp | Suggested: e2e4
[BRAIN SIGNAL] opportunity | high | Eval: 250cp | Delta: 220cp | Suggested: d4d5
[BRAIN SIGNAL] blunder | high | Eval: -180cp | Delta: -250cp
[BRAIN SIGNAL] danger | medium | Eval: -120cp | Delta: -110cp
```

If `evalDelta` is 0, the `Delta:` segment is omitted.
If `suggestedMove` is null, the `Suggested:` segment is omitted.

**Note:** For high-urgency hints, a second hint may follow shortly after with LLM-generated commentary replacing the summary (e.g., "White's knight fork is devastating here"). The signal and urgency stay the same.

**Source:** `useChessVoiceAgent.ts` lines 352-359

### 3c. Player Thinking Pause

**Trigger:** 15 seconds of silence after the last Brain analysis completes (no user speech, no moves).
**Timer resets on every completed analysis (i.e., every move).**

**Format:**
```
[PLAYER THINKING] Player idle 15s+. No user speech detected.
```

**Behavior:** The agent's system prompt tells it to stay quiet during thinking pauses unless there's a genuinely urgent hint to share. This injection lets the agent know the player is thinking, NOT that it should speak.

**Source:** `useChessVoiceAgent.ts` lines 260-278

### 3d. Session Closing

**Trigger:** `remainingSeconds` drops below 120 (2 minutes left in demo session).
**Fires once per session.** Deferred if agent is currently speaking (fires 1500ms after speech ends).

**Format:**
```
[SESSION CLOSING] ~2min remaining. Deliver closing pitch now.
```

**Behavior:** The agent's system prompt contains a scripted closing pitch to deliver when this signal arrives.

**Source:** `useChessVoiceAgent.ts` lines 280-324

### Summary of All Contextual Updates

| Tag | When | Frequency | Agent should... |
|-----|------|-----------|-----------------|
| `[GAME START]` | On connect (500ms delay) | Once per session | Greet player by name, ask skill level |
| `[BRAIN SIGNAL]` | After interesting move | Per interesting move | Comment if high urgency, note if medium, ignore if low during silence |
| `[PLAYER THINKING]` | 15s of silence post-move | Per silence window | Stay quiet (system prompt rules) |
| `[SESSION CLOSING]` | At 120s remaining | Once per session | Deliver closing pitch immediately |

---

## 4. Tool Output Schemas

All 5 client tools return JSON strings. The voice agent parses these to understand what happened.

### 4a. captureBoard

**Parameters:** None

**Success response:**
```json
{
  "success": true,
  "image": "data:image/png;base64,iVBOR...",
  "message": "Board captured from snapshot reel",
  "moveIndex": 5
}
```

**Fallback response (on-demand capture):**
```json
{
  "success": true,
  "image": "data:image/png;base64,iVBOR...",
  "message": "Board captured on-demand"
}
```

**Error response:**
```json
{
  "success": false,
  "error": "Board capture not available"
}
```

### 4b. getBestMove

**Parameters:** None (reads from Brain cache)

**Success response:**
```json
{
  "success": true,
  "partial": false,
  "evaluation": 150,
  "assessment": "Slight advantage",
  "sideToMove": "white",
  "playerColor": "white",
  "bestMove": "e2e4",
  "from": "e2",
  "to": "e4",
  "piece": "pawn",
  "pieceColor": "white",
  "description": "Move white pawn from e2 to e4",
  "hint": "Good move, +120cp swing",
  "signal": "opportunity"
}
```

**Assessment scale:**
| Evaluation (cp) | Assessment |
|-----------------|------------|
| > 500 | Winning position |
| > 200 | Clear advantage |
| > 50 | Slight advantage |
| > -50 | Equal position |
| > -200 | Slight disadvantage |
| > -500 | Clear disadvantage |
| <= -500 | Losing position |

**Partial response (analysis still in progress):**
```json
{
  "success": true,
  "partial": true,
  "evaluation": 0,
  "assessment": "Equal position",
  "sideToMove": "black",
  "playerColor": "white"
}
```

**Error response:**
```json
{
  "success": false,
  "error": "No analysis available yet -- Brain is still computing"
}
```

### 4c. getGameState

**Parameters:** None

**Success response:**
```json
{
  "success": true,
  "fen": "rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2",
  "activeColor": "b",
  "moveHistory": ["e4", "e5", "Nf3"],
  "lastMove": "Nf3",
  "userColor": "w",
  "isCheck": false,
  "isCheckmate": false,
  "isStalemate": false,
  "isDraw": false,
  "isGameOver": false,
  "capturedPieces": {
    "white": [],
    "black": []
  },
  "moveNumber": 2,
  "brain": {
    "moveCount": 3,
    "lastAnalysis": {
      "fen": "rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2",
      "bestMove": { "from": "b8", "to": "c6" },
      "evaluation": 30,
      "hint": null,
      "timestamp": 1708000000000,
      "isComplete": true,
      "moveIndex": 3
    },
    "hasOpeningSuggestions": true,
    "snapshotCount": 3
  },
  "evaluation": 30,
  "bestMove": { "from": "b8", "to": "c6" },
  "hint": null
}
```

**Error response:**
```json
{
  "success": false,
  "error": "No game state available"
}
```

### 4d. highlightSquares

**Parameters:**
```json
{
  "squares": "e2e4"
}
```

Accepts: single square (`"e4"`), move notation (`"e2e4"` or `"e2-e4"`).

**Single square response:**
```json
{
  "success": true,
  "type": "square",
  "squares": ["e4"],
  "message": "Highlighted e4"
}
```

**Move highlight response:**
```json
{
  "success": true,
  "type": "move",
  "squares": ["e2", "e4"],
  "from": "e2",
  "to": "e4",
  "message": "Highlighted move e2 to e4"
}
```

**Error response:**
```json
{
  "success": false,
  "error": "Invalid format. Use single square (e4) or move notation (e2e4 or e2-e4)"
}
```

### 4e. PlayMusic

**Parameters:**
```json
{
  "action": "play",
  "trackId": "track-1"
}
```

Actions: `list`, `play`, `pause`, `next`, `stop`. `trackId` is optional (only for `play`).

**List response:**
```json
{
  "success": true,
  "action": "list",
  "tracks": [
    { "id": "track-1", "name": "Lofi Beats", "artist": "ChillHop" },
    { "id": "track-2", "name": "Focus Flow", "artist": "Ambient Labs" }
  ],
  "message": "2 tracks available"
}
```

**Play response:**
```json
{
  "success": true,
  "action": "play",
  "track": {
    "id": "track-1",
    "name": "Lofi Beats",
    "artist": "ChillHop"
  },
  "message": "Now playing: Lofi Beats by ChillHop"
}
```

**Pause response:**
```json
{
  "success": true,
  "action": "pause",
  "message": "Music paused"
}
```

**Next response:**
```json
{
  "success": true,
  "action": "next",
  "track": {
    "id": "track-2",
    "name": "Focus Flow",
    "artist": "Ambient Labs"
  },
  "message": "Now playing: Focus Flow by Ambient Labs"
}
```

**Stop response:**
```json
{
  "success": true,
  "action": "stop",
  "message": "Music stopped"
}
```

---

## 5. Brain Type Definitions

These are the core TypeScript types that define the Brain pipeline. Copy these into any new implementation.

### InterestSignal
```typescript
type InterestSignal = "danger" | "opportunity" | "blunder" | "brilliant" | "opening" | "none";
```

### Urgency
```typescript
type Urgency = "high" | "medium" | "low";
```

### BrainHint
```typescript
interface BrainHint {
  signal: InterestSignal;
  urgency: Urgency;
  summary: string;          // Human-readable description (or LLM commentary for high-urgency)
  suggestedMove?: string;   // UCI notation, e.g. "e2e4"
  evaluation: number;       // Centipawns from white's perspective
  evalDelta?: number;       // Change from previous position
}
```

### BrainAnalysis
```typescript
interface BrainAnalysis {
  fen: string;
  bestMove: { from: string; to: string } | null;
  evaluation: number;       // Centipawns
  hint: BrainHint | null;
  timestamp: number;        // Date.now()
  isComplete: boolean;      // false while worker is computing
  moveIndex: number;
}
```

### BrainSnapshot
```typescript
interface BrainSnapshot {
  moveIndex: number;
  imageDataUrl: string;     // data:image/png;base64,...
  timestamp: number;
}
```

### ChessBoardState (game state from board component)
```typescript
interface ChessBoardState {
  fen: string;
  activeColor: "w" | "b";
  moveHistory: string[];    // SAN notation
  lastMove: string | null;
  userColor: "w" | "b";
  isCheck: boolean;
  isCheckmate: boolean;
  isStalemate: boolean;
  isDraw: boolean;
  isGameOver: boolean;
  capturedPieces: {
    white: string[];        // Pieces white has captured
    black: string[];        // Pieces black has captured
  };
  moveNumber: number;
  boardImage?: string;      // base64 PNG, populated on demand
}
```

---

## 6. Interest Detection Thresholds

The Brain classifies position changes by evaluation swing (centipawns delta from previous position):

| Eval Delta | Signal | Urgency | Agent Behavior |
|-----------|--------|---------|----------------|
| Move 1 (any) | `opening` | medium | Always comment on first move |
| > +200cp | `opportunity` | high | Speak up, LLM commentary triggered |
| < -200cp | `blunder` | high | Speak up, LLM commentary triggered |
| > +100cp | `opportunity` | medium | Note it, comment if player engages |
| < -100cp | `danger` | medium | Note it, comment if player engages |
| -100 to +100cp | (none) | -- | Routine move, stay quiet |

**LLM commentary** is only triggered for `high` urgency signals. The commentary replaces the auto-generated summary with a natural-language sentence from GPT-4o-mini.

---

## Source File Index

| File | What it contains |
|------|-----------------|
| `src/chess-demo/services/chess-brain.ts` | ChessBrainService singleton, proactive pipeline, interest detection |
| `src/chess-demo/services/brain-types.ts` | All Brain type definitions |
| `src/chess-demo/services/voice-agent-tools.ts` | 5 client tool implementations + schemas |
| `src/chess-demo/hooks/useChessVoiceAgent.ts` | Voice agent hook, prompt injections, Brain lifecycle |
| `src/chess-demo/types/index.ts` | ChessBoardState, ChessCallbacks, tool types |
| `supabase/functions/chess-brain-analysis/index.ts` | LLM edge function (commentary + openings) |
