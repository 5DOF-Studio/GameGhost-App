# Vision Context Engineering Strategy

## Source Research
`Vision Model Attention and Context Engineering for Game Screen Capture` (March 2026)

## Core Insight
Vision models perform perceptual grouping, not human-like attention. They attend to textures and patterns, not structurally obvious game state elements. Raw screenshots sent to raw prompts will always underperform. The highest-impact interventions are preprocessing and prompt engineering, not model selection alone.

## Model Comparison for Chess Vision

| Model | Strengths | Weaknesses | Token Cost (1920x1080) |
|-------|-----------|------------|----------------------|
| GPT-4o | Structured text extraction, JSON conformance, OCR accuracy | Spatial reasoning, tile boundary splits UI | ~1,105 tokens |
| Claude Sonnet | Interpretive reasoning, uncertainty honesty, complex layouts | Spatial localization, speed (~400ms) | ~1,600 tokens |
| Gemini 2.5 Flash | #1 LMArena Vision, native multimodal, resolution control, 90% caching | Historical over-elaboration (improved in 2.5+) | 280-2,240 tokens (configurable) |
| Gemini 2.5 Pro | Best spatial reasoning, Agentic Vision (auto zoom/crop) | Cost, latency | HIGH res: 1,120 tokens |

**Decision:** Switch brain model to Gemini via OpenRouter for chess vision. Gemini's native multimodal architecture, spatial reasoning, resolution control, and context caching make it the best fit for game screen analysis. Claude remains valuable for interpretive reasoning and can serve as validation model.

## Strategies — Immediate (Phase 16)

### 1. Chain-of-Thought Vision Prompting
**Impact:** 9.13% hallucination reduction (Visual Inference Chain, 2024)
**Implementation:** Two-step prompt: "First describe exactly what you see on the board. Then provide your analysis."
**Effort:** Prompt-only change in BrainPromptBuilder

### 2. UNREADABLE Escape Hatch
**Impact:** Single most important prompt technique for reducing confabulation
**Implementation:** Add to system prompt: "If you cannot clearly identify a piece or position, output UNREADABLE rather than guessing. An incorrect reading is worse than admitting uncertainty."
**Effort:** One paragraph in BrainPromptBuilder

### 3. Confidence Calibration
**Impact:** Enables downstream filtering of low-confidence claims
**Implementation:** Require CERTAIN (>95%), LIKELY (75-95%), UNCERTAIN (50-75%), GUESSING (<50%) tags. BrainEventRouter can optionally suppress GUESSING-level claims.
**Effort:** Prompt change + optional parsing in BrainEventRouter

### 4. Image Resolution Optimization
**Impact:** Reduce upload size, faster API calls, stay within model-optimal resolution
**Current state:** 2218x2170 raw capture -> ScaleAndCompress at 0.5x -> ~1109x1085 JPEG. This is already under Claude's 1568px threshold.
**For Gemini:** Use `media_resolution: HIGH` (1,120 tokens) via OpenRouter provider preferences. Captures are already reasonably sized.
**Effort:** Add provider preferences to OpenRouter request if needed

### 5. Gemini Brain Model Switch
**Impact:** Better spatial reasoning for chess boards, faster response (~200ms vs ~400ms), context caching eligibility
**Implementation:** Change model string from `anthropic/claude-sonnet-4` to `google/gemini-2.5-flash` in OpenRouterBrainService/MauiProgram.
**Verification:** Ensure OpenRouter request format (messages, image_url, tools) works with Gemini.
**Effort:** Config change + compatibility test

### 6. Structured JSON Output Schema
**Impact:** Eliminates free-text parsing ambiguity, enables programmatic confidence checking
**Implementation:** Add `response_format: { type: "json_schema", json_schema: {...} }` to OpenRouter request. Define schema with fields: last_move, position_assessment, threats, suggested_action, fen, confidence.
**Effort:** Moderate -- touches OpenRouterClient, request DTOs, BrainEventRouter parsing

### 7. Temporal Consistency Validation
**Impact:** Catches impossible state transitions (hallucinated castling, phantom captures)
**Implementation:** GameJournalService already tracks position history. Add validation: compare brain's claimed move/position against previous journal entries. Flag contradictions.
**Effort:** Moderate -- new validation method in GameJournalService or BrainEventRouter

### 8. Switch Default Voice Provider to Gemini
**Impact:** Unblocks voice testing (OpenAI Realtime returning empty responses)
**Implementation:** Change default provider selection or env var configuration
**Effort:** Config change

## Strategies — Deferred (Future Phases)

### Multi-Model Validation
Send same screenshot to 2+ models via OpenRouter. Compare outputs. Disagreements trigger escalation. The research calls this "the single biggest accuracy improvement" at 2-3x cost. Defer until single-model accuracy is optimized.

### Gemini Context Caching
90% discount on cached tokens for repeated similar screenshots. Requires Gemini-specific API (`cachedContent` resource) which may not be exposed through OpenRouter. Investigate API support first.

### Pre-Crop Known UI Regions
Crop chess board area from full window capture before sending. Eliminates title bar, sidebar, and chrome. Requires per-game crop region configuration. Already have CropRect on IFrameDiffService.

### Set-of-Mark Annotation
Overlay numbered/lettered labels at known board squares (a1-h8) on the image before sending. Boosted GPT-4V accuracy from 70.5% to 93.8% in research. For chess the grid is fixed and known. Complex but high-impact.

### Hybrid OCR + VLM Pipeline
Run local OCR (Tesseract/EasyOCR) in parallel with VLM calls. Cross-check results. Local OCR adds 5-50ms and catches cases VLM gets wrong. Particularly useful for numeric values.

### Resolution Escalation
When brain reports low confidence on a region, auto-crop and resubmit at higher resolution. Gemini's Agentic Vision does this natively.

## Key Research Findings

1. **ViT patch size determines what survives tokenization.** Details smaller than ~12px in original image are unreliable across all models.
2. **Models are texture-biased, not shape-biased.** Game environments (grass, stone, wood textures) compete with flat UI elements for attention budget.
3. **GPT-4o tile boundaries can split UI elements.** Elements straddling 512px boundaries break spatial coherence.
4. **Image placement matters.** Anthropic recommends images before text. OpenAI/OpenRouter recommend text before images. For cross-model via OpenRouter: system prompt first, then image, then extraction query.
5. **Most production gaming tools don't use screen capture for game state.** They use game APIs (Riot Games API, Overwolf Game Events). Screen capture is the hard path -- but the only universal one.
6. **Confidence calibration varies by model.** GPT models have better verbalized confidence calibration. Claude is more honestly conservative. Cross-model agreement is the most reliable signal.
