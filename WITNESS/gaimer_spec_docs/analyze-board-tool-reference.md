# Analyze Board Tool — Design Reference

## Context
Reference for building Leroy's (Chess Agent) analyze-board tool. The goal is fast, structured position analysis that the AI can read and relay to the user in natural language.

## Key Insight
Stockfish isn't slow at shallow depths — depth 12-15 runs in milliseconds at ~2800+ Elo (stronger than any human). Slowness only appears at depth 25+ for deep analysis. But self-hosting an engine isn't necessary when free cloud APIs exist.

---

## Recommended Architecture: Lichess API Aggregator

A thin service that takes a FEN and hits 2-3 Lichess endpoints in parallel, returning a structured analysis object.

### Data Sources

| Source | What it gives you | Speed |
|--------|-------------------|-------|
| Lichess Cloud Eval | Engine eval + best moves (pre-computed Stockfish) | Instant (cached) |
| Lichess Opening Explorer | "This move is played 45% of the time, wins 52%" | Instant |
| Lichess Masters DB | "Carlsen played this 3 times" | Instant |
| Endgame Tablebases | "White wins in 23 moves" (7 pieces or fewer) | Instant |

### API Endpoints

```
# Cloud evaluation (pre-computed Stockfish)
GET https://lichess.org/api/cloud-eval?fen=<FEN>&multiPv=3
Response: top 3 moves with eval scores, depth, nodes

# Opening explorer (game statistics from millions of games)
GET https://explorer.lichess.ovh/lichess?fen=<FEN>
Response: move stats (win%, draw%, loss%) from real games

# Masters database
GET https://explorer.lichess.ovh/masters?fen=<FEN>
Response: how titled players have played this position

# Endgame tablebases (perfect play for <=7 pieces)
GET https://tablebase.lichess.ovh/standard?fen=<FEN>
Response: optimal move, DTZ (distance to zeroing), winner
```

### Example AI-Readable Output

Combined response the AI agent could interpret:

> "The position is +1.2 in White's favor. The best move is Nf3 (played in 62% of master games, 55% win rate). Black's best response is d5. In Carlsen's games from this position, he scored 2.5/3."

This is far more useful to a user than raw engine lines.

---

## Tool Schema (Draft)

```json
{
  "name": "analyze_board",
  "description": "Analyze current chess position from screen capture",
  "parameters": {
    "fen": "string — FEN notation of the current position",
    "depth": "string — 'quick' (cloud eval only) or 'full' (all sources)",
    "perspective": "string — 'white' or 'black'"
  },
  "returns": {
    "evaluation": "float — centipawn evaluation (positive = white advantage)",
    "best_moves": "array — top 3 moves with eval and continuation",
    "opening_stats": "object — win/draw/loss percentages, most common moves",
    "master_games": "array — notable games from this position",
    "endgame_result": "object — tablebase result if <=7 pieces",
    "narrative": "string — natural language summary for the AI to relay"
  }
}
```

## Implementation Notes

- No Docker, no compute costs, no cold starts
- All Lichess APIs are free and don't require authentication for reasonable usage
- Rate limits: ~1 request/second (sufficient for real-time game analysis)
- FEN extraction from screen capture is a separate concern (OCR/vision model)
- Consider caching responses locally since positions repeat in analysis

## Alternatives Considered

| Option | Pros | Cons |
|--------|------|------|
| Self-hosted Stockfish (depth 12) | Full control, offline | Requires compute, packaging |
| Lichess Cloud APIs | Free, instant, rich data | Requires internet, rate limited |
| chess.com API | Large DB | Requires auth, less open |
| Leela Chess Zero (Lc0) | Neural net eval | Heavy GPU requirement |

**Decision: Lichess Cloud APIs** — zero infrastructure, instant responses, richer context than raw engine output.
