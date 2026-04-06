# Structured Brain Output Hardening Plan

**Project:** Gaimer Desktop (.NET MAUI)  
**Date:** 2026-03-19  
**Status:** Planned  
**Scope:** Brain analysis parsing reliability, markdown/header leakage prevention, chess visibility prompt alignment verification, and env-gated live image-analysis regression coverage

---

## Summary

Structured brain analysis was implemented so chess image-analysis output could be split into semantically distinct timeline events instead of rendering as one large text blob. That implementation works only when the provider returns strict JSON matching `BrainAnalysisResult`.

Observed real-world behavior indicates that the provider can still return pseudo-structured markdown text such as:

```text
**VISUAL DESCRIPTION:** ...
**POSITION ASSESSMENT:** ...
**THREATS:** ...
**SUGGESTED ACTION:** ...
```

When that happens, the current code falls back to a single `ImageAnalysis` event and leaks the formatting artifacts directly into the UI.

This plan defines the work needed to harden the system so:

1. strict JSON still works
2. markdown-style structured text is heuristically recovered
3. raw `**...**` markers do not leak into timeline/top-strip/journal surfaces
4. one env-gated live image-analysis test verifies the best guarantee we can reasonably expect in production

---

## Problem Statement

### Intended behavior

When the brain returns structured analysis, the app should emit separate timeline events for:

- visual description
- position assessment
- threats
- suggested action

This behavior is implemented in [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs#L100) and documented in [chronicles/DECISION_LOG.md](/Users/tonynlemadim/Developer/gAImer_desktop/chronicles/DECISION_LOG.md#L25).

### Actual failure mode

If the returned text is not strict JSON, `TryParseStructuredAnalysis(...)` returns `null`, and the router falls back to:

- a single `ImageAnalysis` timeline event
- unsanitized `AnalysisText`
- unsanitized top-strip text
- unsanitized journal description fallback

See:

- parser entry at [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs#L356)
- fallback branch at [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs#L375)
- parser implementation at [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs#L564)

### Confirmed known bug

This is already reflected in repository continuity docs as:

- `BUG-011`: CoT `**VISUAL DESCRIPTION:**` markers leak to timeline on JSON parse failure

See:

- [chronicles/HANDOFF.md](/Users/tonynlemadim/Developer/gAImer_desktop/chronicles/HANDOFF.md#L62)
- [STATE.md](/Users/tonynlemadim/Developer/gAImer_desktop/.planning/STATE.md#L102)

---

## Current Implementation Review

## What already works

### 1. Strict JSON schema is requested from the provider

`OpenRouterClient` requests `response_format` with `strict: true` and fields:

- `visual_description`
- `last_move`
- `position_assessment`
- `threats`
- `suggested_action`
- `fen`
- `confidence`

See [OpenRouterClient.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/Brain/OpenRouterClient.cs#L176).

### 2. Valid JSON is parsed into `BrainAnalysisResult`

`TryParseStructuredAnalysis(...)` deserializes valid JSON into [BrainAnalysisResult.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Models/BrainAnalysisResult.cs#L1).

### 3. Structured analysis is distributed into multiple timeline events

`OnStructuredAnalysis(...)` maps fields to event types:

- `VisualDescription` -> `ImageAnalysis`
- `PositionAssessment` -> `Assessment`
- `Threats` -> `Danger`
- `SuggestedAction` -> `SageAdvice`

See [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs#L100).

### 4. There is good happy-path unit coverage

Existing tests cover:

- valid JSON parse
- invalid JSON returns null
- free text returns null
- structured distribution emits multiple events
- unstructured text falls back to a single event

See:

- [BrainAnalysisResultTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Brain/BrainAnalysisResultTests.cs#L68)
- [BrainEventRouterTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Brain/BrainEventRouterTests.cs#L850)

## What does not yet work

### 1. No heuristic parser for markdown-labeled pseudo-structure

If the model returns headings instead of JSON, the app does not attempt recovery.

### 2. No sanitizer for markdown emphasis/header leakage

The fallback path currently forwards raw text to:

- timeline event full content
- top strip
- journal fallback text

### 3. No live image-analysis regression test for structured-or-safe output

The repo has env-gated live API tests already, but not one that sends a real chess image and asserts structured compliance or safe degradation.

---

## Goals

1. **Preserve the strict JSON path**
   Do not regress the current happy path.

2. **Recover useful structure from markdown-labeled fallback output**
   If the provider returns headings instead of JSON, split that output into the same semantic sections whenever possible.

3. **Block raw markdown artifacts from reaching users**
   Timeline, top strip, and journal should not display `**VISUAL DESCRIPTION:**` or similar formatting debris.

4. **Add high-value tests that catch the real failure mode**
   Tests must cover both strict JSON and pseudo-structured markdown.

5. **Add one live image-analysis regression test**
   Use a real chess image and assert the provider output is either parseable JSON or safely recoverable/sanitized.

---

## Non-Goals

1. Exact deterministic wording from the model.
2. Provider-specific prompt tuning as the only fix.
3. Removing `visual_description` from the schema. The field is still useful for grounding.
4. Turning all live API tests into CI-required checks.

---

## Proposed Design

## Layer 1: Strict JSON First

Keep the current strict JSON path unchanged:

1. attempt `TryParseStructuredAnalysis(text)`
2. if successful, emit structured events

This remains the preferred path.

## Layer 2: Heuristic Labeled-Section Recovery

If strict JSON parsing fails, run a secondary parser that recognizes labeled sections in plain text.

### Supported labels

The heuristic parser should recognize common variants such as:

- `VISUAL DESCRIPTION:`
- `POSITION ASSESSMENT:`
- `THREATS:`
- `SUGGESTED ACTION:`
- `LAST MOVE:`
- `FEN:`
- `CONFIDENCE:`

It should tolerate:

- surrounding markdown emphasis like `**LABEL:**`
- extra whitespace
- upper/lower/mixed case
- optional blank lines between sections

### Output

The heuristic parser should produce a `BrainAnalysisResult` when enough fields are recoverable to be useful.

Minimum useful recovery:

- `position_assessment` alone, or
- any two among `visual_description`, `position_assessment`, `threats`, `suggested_action`

### Why this approach

This gives the app a second chance to recover intended structure without pretending the provider will always obey strict JSON perfectly.

## Layer 3: Display Sanitization

If both strict JSON parse and heuristic parse fail, sanitize before display.

### Sanitization requirements

Remove or normalize:

- double-asterisk emphasis markers
- markdown heading wrappers around section labels
- repeated leading formatting tokens
- obviously prompt-internal labels that do not belong in the UI

### Surfaces that must use sanitized text

1. `OnImageAnalysis(...)` fallback event
2. top strip fallback text
3. journal fallback description

### Important rule

Users should never see raw prompt-contract syntax.

---

## Proposed Code Changes

## 1. Add a structured fallback helper module

Add a helper near `BrainEventRouter` or in `Services/` with responsibilities:

- `TryParseStructuredAnalysisJson(string text)`
- `TryParseStructuredAnalysisLabeledText(string text)`
- `SanitizeStructuredAnalysisFallback(string text)`

Possible filename:

- `src/WitnessDesktop/WitnessDesktop/Services/StructuredBrainAnalysisParser.cs`

### Notes

- Keep the JSON path simple and explicit.
- Keep labeled-text parsing deterministic and regex-based.
- Keep sanitization separate so it can be reused across timeline/top-strip/journal surfaces.

## 2. Update `BrainEventRouter`

Current flow:

- strict JSON parse or single-event fallback

New flow:

1. try strict JSON parse
2. if null, try labeled-text parse
3. if either succeeds, call `OnStructuredAnalysis(...)`
4. otherwise sanitize text before fallback `OnImageAnalysis(...)`
5. sanitize fallback top-strip text
6. sanitize fallback journal text

### Result

The user should either get:

- split structured events, or
- one clean, readable fallback event

Never one raw markdown blob with stray asterisks.

## 3. Optional top-strip selection rule

When structured data is available:

- top strip should continue using `PositionAssessment`

When only sanitized fallback text is available:

- top strip should use sanitized + truncated text

## 4. Journal sanitization

Current journal fallback:

- `structured?.ToDisplayText() ?? result.AnalysisText`

New fallback:

- `structured?.ToDisplayText() ?? SanitizeStructuredAnalysisFallback(result.AnalysisText)`

This avoids preserving raw prompt labels in stored history.

---

## Test Plan

## Unit Tests: Parser and Sanitizer

Add dedicated tests for the new helper.

### Test group A: labeled markdown parsing

Input:

```text
**VISUAL DESCRIPTION:** White pieces control the center.
**POSITION ASSESSMENT:** White is slightly better.
**THREATS:** Knight fork on f7.
**SUGGESTED ACTION:** Develop Nf3.
```

Assert:

- heuristic parse succeeds
- fields map correctly
- no raw `**` remain in recovered content

### Test group B: label variants

Input variants:

- `Visual Description:`
- `POSITION ASSESSMENT :`
- `Threats:`
- `Suggested Action:`

Assert:

- parser remains case- and whitespace-tolerant

### Test group C: partial pseudo-structure

Input with only two usable sections.

Assert:

- parse succeeds if threshold met
- only present sections emit events

### Test group D: sanitization-only fallback

Input:

```text
**VISUAL DESCRIPTION:** unclear board state
**NOTES:** model uncertain
```

If heuristic parse intentionally fails, assert:

- sanitized text contains no `**`
- sanitized text does not contain raw `VISUAL DESCRIPTION:` heading in user-facing fallback form if policy chooses to strip it

## Integration Tests: `BrainEventRouter`

Add new tests beside existing structured analysis tests in [BrainEventRouterTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Brain/BrainEventRouterTests.cs#L850).

### Required router tests

1. `RouteBrainResult_MarkdownStructuredText_EmitsMultipleEvents`
2. `RouteBrainResult_MarkdownStructuredText_TopStripUsesAssessment`
3. `RouteBrainResult_UnstructuredFallback_SanitizesAsterisks`
4. `RouteBrainResult_UnstructuredFallback_JournalUsesSanitizedText`

### Why these matter

The existing tests prove the strict JSON path. These new tests prove the observed failure mode is now contained.

## Live API Test

### Goal

Add one env-gated live image-analysis regression test that uses a real chess image and asserts the provider output is either:

1. valid structured JSON, or
2. recoverable/sanitizable into safe structured display output

### Test asset

Use:

- [sampleImage.png](/Users/tonynlemadim/Developer/gAImer_desktop/WITNESS/gaimer_spec_docs/ui_mockup/sampleImage.png)

If that image turns out not to be suitable for real chess-board analysis, replace it with a purpose-specific test asset under a stable test-data location such as:

- `src/WitnessDesktop/WitnessDesktop.Tests/TestData/chess_sample_position.png`

### Suggested test file

- extend [LiveApiTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Integration/LiveApiTests.cs#L11), or
- add `LiveImageAnalysisTests.cs`

### Suggested test name

- `OpenRouter_ImageAnalysis_SampleChessImage_ReturnsStructuredOrSanitizedOutput`

### Env gating

Use the same pattern as the existing live tests:

- skip if `OPENROUTER_APIKEY` is absent
- do not run in normal CI by default
- tag with `[Trait("Category", "LiveApi")]`

### Acceptance rule for the live test

The test passes if all of the following are true:

1. response is non-empty
2. either strict JSON parse succeeds or labeled-text parse succeeds or sanitization produces safe fallback text
3. final display-safe text contains no raw `**VISUAL DESCRIPTION:**`-style markers
4. if structure is recoverable, at least `PositionAssessment` or one meaningful semantic section is present

### Why this is the right guarantee

This is the best realistic business-practice guarantee:

- we do not require exact wording
- we do require structural compliance or safe degradation
- we use a fixed image artifact
- we catch the actual UX failure the user observed

---

## Slice Plan for Claude

## Slice 1: Parser and sanitizer design

### Scope

- add parser helper
- add JSON parse wrapper
- add labeled-text parser
- add sanitization helper

### Acceptance

- helper compiles
- parser unit tests for core cases pass

## Slice 2: Router integration

### Scope

- update `BrainEventRouter` to use fallback parser
- sanitize fallback timeline/top-strip/journal text

### Acceptance

- router tests pass
- markdown-structured input no longer falls through to raw dirty bubble behavior

## Slice 3: Unit/integration coverage expansion

### Scope

- add router tests for markdown structured input
- add tests for asterisk/header leakage removal

### Acceptance

- tests fail before fix, pass after fix

## Slice 4: Live image-analysis regression test

### Scope

- add env-gated live test using sample image
- assert structured-or-sanitized safe output

### Acceptance

- test skips cleanly without env vars
- test provides actionable failure output when structure/sanitization regresses

## Slice 5: Documentation and bug closure

### Scope

- update progress docs if behavior is verified
- re-evaluate `BUG-011`

### Acceptance

- docs reflect actual state
- bug remains open only if live validation still shows leakage

---

## Acceptance Criteria

This work is complete only when:

1. markdown pseudo-structured brain output no longer appears as one raw dirty bubble with `**...**` markers
2. strict JSON output still splits correctly
3. fallback text shown to users is sanitized
4. journal fallback content is sanitized
5. parser/router tests cover markdown-labeled pseudo-structured output
6. one env-gated live image-analysis test exists and verifies structured-or-safe degradation behavior

---

## Risks

### Risk 1: provider output remains inconsistent

Even with strict schema, provider behavior may still drift. That is why the fallback parser and live regression test are both required.

### Risk 2: over-aggressive sanitization

A sanitizer that removes too much could destroy useful content. The implementation must strip formatting artifacts, not semantic content.

### Risk 3: sample image may not be representative enough

If `sampleImage.png` is not a stable chess-board test input, promote a dedicated real-board test asset into a proper test-data location.

---

## Recommended Immediate Next Step

Start with Slice 1 and Slice 2 together:

1. implement parser + sanitizer
2. wire into `BrainEventRouter`
3. add failing tests for markdown-labeled pseudo-structured input

Then add the env-gated live image-analysis test as Slice 4 once the local fallback behavior is correct.

---

## References

- [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs#L356)
- [BrainAnalysisResult.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Models/BrainAnalysisResult.cs#L1)
- [OpenRouterClient.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/Brain/OpenRouterClient.cs#L176)
- [BrainAnalysisResultTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Brain/BrainAnalysisResultTests.cs#L68)
- [BrainEventRouterTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Brain/BrainEventRouterTests.cs#L850)
- [chronicles/DECISION_LOG.md](/Users/tonynlemadim/Developer/gAImer_desktop/chronicles/DECISION_LOG.md#L25)
- [sampleImage.png](/Users/tonynlemadim/Developer/gAImer_desktop/WITNESS/gaimer_spec_docs/ui_mockup/sampleImage.png)

