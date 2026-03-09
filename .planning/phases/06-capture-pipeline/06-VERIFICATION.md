---
phase: 06-capture-pipeline
verified: 2026-03-02T21:21:40Z
status: passed
score: 10/10 must-haves verified
gaps: []
---

# Phase 06: Capture Pipeline — Brain-Voice Alignment Verification Report

**Phase Goal:** Enforce brain-voice pipeline rules (core IP). Remove legacy voice-receives-images path. Implement IFrameDiffService (dHash with SkiaSharp) for smart frame submission. Build L1/L2 context layers in IBrainContextService. Wire voice to pull context from brain, not receive raw images. Capture precepts (Auto/Diff/OnDemand).
**Verified:** 2026-03-02T21:21:40Z
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Voice NEVER receives raw image bytes -- SendImageAsync is not called from the capture pipeline | VERIFIED | `grep _conversationProvider.SendImageAsync` across entire codebase returns zero call-site matches. The only occurrence in MainViewModel.cs is a comment at line 251 confirming removal. |
| 2 | Brain is the sole consumer of captured frames | VERIFIED | MainViewModel.cs FrameCaptured handler (lines 201-254) routes compressed frames exclusively to `_brainService.SubmitImageAsync` (line 245). No other path exists. |
| 3 | Frames that have not materially changed (dHash threshold) are not submitted to brain | VERIFIED | MainViewModel.cs line 232: `if (!_frameDiffService.HasChanged(compressed))` returns early before brain submission. Diff gate is correctly positioned after compression but before SubmitImageAsync. |
| 4 | dHash perceptual hash computed from image bytes via SkiaSharp | VERIFIED | FrameDiffService.cs lines 24-45: `SKBitmap.Decode` + `Resize(9x8, Gray8)` + horizontal pixel comparison producing 64-bit hash. Uses `BitOperations.PopCount` for Hamming distance (line 82). |
| 5 | HasChanged returns false when frame is visually identical (debounce enforced) | VERIFIED | FrameDiffService.cs lines 86-113: threshold check (default=10), debounce via `_lastChangeTime` with 1.5s `_debounceWindow`. Updates `_lastHash` on every call regardless of change detection. |
| 6 | BrainResults flowing through the router are ingested as L1 events in the context service | VERIFIED | BrainEventRouter.cs lines 241-273: After the switch block, creates `BrainEvent` from each `BrainResult` with type/category mapping and calls `_brainContext.IngestEventAsync`. Null-safe via `_brainContext != null` guard. |
| 7 | L1 events older than 30 seconds are excluded from voice/chat context envelopes | VERIFIED | BrainContextService.cs lines 158-167: L1 query uses `requestTsUtc - L1Window` (30s) as cutoff, with confidence >= 0.3 and staleness filters. |
| 8 | L2 rolling summary is rebuilt from recent L1 events when queried | VERIFIED | BrainContextService.cs lines 232-267: L2 events queried from 30s-5min window, passed to `BuildL2Summary` (lines 315-333) which groups by category, counts, shows latest text per category. |
| 9 | Context envelopes include ImmediateEvents populated from the L1 store | VERIFIED | BrainContextService.cs line 300: `ImmediateEvents = l1Included` (budget-trimmed list from L1 store, not empty array). Line 301: `RollingSummary = rollingSummary` (generated text, not empty). |
| 10 | IFrameDiffService is registered in DI and injected into MainViewModel | VERIFIED | MauiProgram.cs line 110: `services.AddSingleton<IFrameDiffService, FrameDiffService>()`. MainViewModel constructor (line 174): `IFrameDiffService frameDiffService` parameter. IBrainContextService passed to BrainEventRouter at MauiProgram.cs line 124. |

**Score:** 10/10 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Services/IFrameDiffService.cs` | Interface for frame diff detection | VERIFIED (45 lines) | Exports IFrameDiffService, CropRect, FrameChangeEventArgs. Full API: ComputeHash(2 overloads), CompareHashes, HasChanged, FrameChanged event. |
| `Services/FrameDiffService.cs` | dHash implementation with SkiaSharp | VERIFIED (114 lines) | Sealed class implementing IFrameDiffService. Uses SkiaSharp decode+resize, BitOperations.PopCount, 1.5s debounce. Zero TODO/FIXME/placeholder patterns. |
| `Services/IBrainContextService.cs` | Extended interface with IngestEventAsync | VERIFIED (42 lines) | Has IngestEventAsync(BrainEvent, CancellationToken) alongside GetContextForVoiceAsync, GetContextForChatAsync, FormatAsPrefixedContextBlock. |
| `Services/BrainContextService.cs` | L1 store + L2 summary + upgraded envelope | VERIFIED (364 lines) | Lock-protected L1 store (_l1Events), 5min retention + 200 cap, L1/L2 time-window queries, budget-aware envelope assembly, deterministic L2 summary generation, dynamic EnvelopeConfidence. Zero TODO/FIXME. |
| `Services/BrainEventRouter.cs` | L1 event ingestion on each routed BrainResult | VERIFIED (334 lines) | Constructor accepts IBrainContextService? (nullable, backward-compatible). RouteBrainResult creates BrainEvent with type/category mapping and calls IngestEventAsync. Voice forwarding sends text only via SendContextualUpdateAsync. |
| `MauiProgram.cs` | DI registrations for new services | VERIFIED (202 lines) | IFrameDiffService registered as singleton (line 110). BrainEventRouter factory resolves IBrainContextService and passes it (lines 114-125). |
| `ViewModels/MainViewModel.cs` | Corrected capture pipeline | VERIFIED | IFrameDiffService injected (line 174, stored line 25). FrameCaptured handler: diff gate (line 232), brain-only submission (line 245), SendImageAsync removed (only comment at line 251). |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| MainViewModel.FrameCaptured | IFrameDiffService.HasChanged | Diff gate before brain | WIRED | Line 232: `if (!_frameDiffService.HasChanged(compressed))` -- returns early if unchanged |
| MainViewModel.FrameCaptured | IBrainService.SubmitImageAsync | Brain-only path | WIRED | Line 245: `_brainService.SubmitImageAsync(compressed, contextStr, ...)` -- sole visual data consumer |
| BrainEventRouter.RouteBrainResult | IBrainContextService.IngestEventAsync | L1 event creation | WIRED | Line 269: `_brainContext.IngestEventAsync(brainEvent)` -- creates BrainEvent from each BrainResult |
| MauiProgram.RegisterServices | FrameDiffService | DI singleton | WIRED | Line 110: `AddSingleton<IFrameDiffService, FrameDiffService>()` |
| MauiProgram.RegisterServices | BrainEventRouter(brainContext) | DI factory | WIRED | Line 118: `sp.GetService<IBrainContextService>()`, line 124: passed to constructor |
| BrainEventRouter | Voice (text only) | SendContextualUpdateAsync | WIRED | Lines 62, 137, 281, 287: All voice paths use `SendContextualUpdateAsync(text)`, never image bytes |
| BrainContextService.BuildEnvelope | L1 events | ImmediateEvents populated | WIRED | Line 300: `ImmediateEvents = l1Included` from budget-trimmed L1 query |
| BrainContextService.BuildEnvelope | L2 summary | RollingSummary populated | WIRED | Line 301: `RollingSummary = rollingSummary` from deterministic category-grouped text |

### Requirements Coverage

| Requirement | Status | Notes |
|-------------|--------|-------|
| Remove legacy SendImageAsync from capture pipeline | SATISFIED | Zero call-site matches for `_conversationProvider.SendImageAsync` in entire codebase |
| Implement IFrameDiffService (dHash with SkiaSharp) | SATISFIED | Full dHash: 9x8 Gray8 resize, 64-bit hash, PopCount Hamming, 1.5s debounce |
| Build L1/L2 context layers in IBrainContextService | SATISFIED | L1 store (30s window, 5min retention, 200 cap), L2 deterministic summary (30s-5min window) |
| Wire voice to pull context from brain, not receive raw images | SATISFIED | Voice receives text via SendContextualUpdateAsync; context available via GetContextForVoiceAsync |
| Capture precepts (Auto/Diff/OnDemand) | SATISFIED | Auto: timer-based (unchanged). Diff: HasChanged gate in FrameCaptured. OnDemand: capture_screen tool triggers brain. |
| Brain is sole consumer of visual data | SATISFIED | Only _brainService.SubmitImageAsync receives frames; no other visual data path exists |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| MauiProgram.cs | 137 | TODO: Remove IGeminiService legacy registration | Info | Pre-existing technical debt from Phase 02, not a Phase 06 issue. Does not affect pipeline architecture. |

### Human Verification Required

### 1. Frame Diff Gate Effectiveness
**Test:** Start a capture session targeting a chess game. Observe debug output for "[Capture] Frame unchanged (dHash) -- skipping brain submission" messages between moves.
**Expected:** When the board is static, brain submissions are skipped. When a piece moves, brain receives the frame.
**Why human:** Diff threshold (10) calibration against real game visuals cannot be verified programmatically.

### 2. End-to-End Brain-to-Voice Pipeline
**Test:** With voice connected, start a capture session. Observe that voice agent speaks brain analysis results (text) without ever receiving image data.
**Expected:** Voice receives text narrations via SendContextualUpdateAsync. No image-related WebSocket messages in voice connection.
**Why human:** Requires live voice connection to verify end-to-end text-only delivery.

### 3. L1/L2 Context Quality
**Test:** After several capture cycles, observe context envelopes via debug logging. Check that ImmediateEvents contains recent brain observations and RollingSummary has category-grouped text.
**Expected:** Non-empty ImmediateEvents (0-30s window) and non-empty RollingSummary (30s-5min window) after sufficient brain analysis cycles.
**Why human:** Requires live session with multiple brain analysis cycles to verify temporal windowing works correctly.

### Gaps Summary

No gaps found. All 10 must-haves are verified. The phase goal is achieved:

1. **Golden Rule enforced:** `_conversationProvider.SendImageAsync` has zero call-site matches in the entire codebase. The legacy anti-pattern is completely removed.
2. **Brain is sole visual consumer:** Only `_brainService.SubmitImageAsync` receives captured frames. All voice paths use text-only `SendContextualUpdateAsync`.
3. **Diff precept operational:** `_frameDiffService.HasChanged(compressed)` gates brain submission with dHash perceptual hashing (9x8 Gray8, 64-bit hash, Hamming distance >= 10, 1.5s debounce).
4. **L1/L2 context layers built:** BrainContextService maintains lock-protected L1 event store (30s window, 5min retention, 200 cap) and generates deterministic L2 rolling summaries (category-grouped text). SharedContextEnvelope.ImmediateEvents and RollingSummary are now populated with real data.
5. **L1 ingestion wired:** BrainEventRouter creates BrainEvent from each routed BrainResult and ingests into IBrainContextService.
6. **DI complete:** IFrameDiffService registered as singleton, IBrainContextService passed to BrainEventRouter factory.

The brain-voice pipeline architecture defined in BRAIN_VOICE_PIPELINE_RULES.md is fully enforced in code.

---

_Verified: 2026-03-02T21:21:40Z_
_Verifier: Claude (gsd-verifier)_
