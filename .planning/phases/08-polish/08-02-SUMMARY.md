---
phase: 08-polish
plan: 02
subsystem: ghost-mode-ui
tags: [swift, appkit, glass-morphism, native, visual-effects]
dependency-graph:
  requires: [phase-04]
  provides: [indigo-glass-morphism-event-cards]
  affects: [08-03, 08-04]
tech-stack:
  added: []
  patterns: [NSVisualEffectView-blur, CAGradientLayer-border-mask, radial-glow-sublayer]
key-files:
  created: []
  modified:
    - src/GaimerDesktop/NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/EventCardView.swift
decisions:
  - id: glass-blur-material
    decision: "Use .hudWindow material with .behindWindow blending for glass effect"
    rationale: "HUD window material provides dark translucent glass matching sci-fi aesthetic; behindWindow blending captures content behind the overlay"
  - id: even-odd-border-mask
    decision: "Use CAShapeLayer even-odd fill rule for gradient border rendering"
    rationale: "Only way to create gradient border effect with CAGradientLayer — outer/inner rounded rect paths create border-only mask"
  - id: accent-color-shift
    decision: "Shift from cyan (#00E4FF) to indigo (#818CF8) accent palette"
    rationale: "Indigo-400/500/600 gradient creates cohesive glass morphism look; cyan was flat and lacked depth"
metrics:
  duration: ~3 minutes
  completed: 2026-03-05
---

# Phase 08 Plan 02: Ghost Mode Event Card Glass Morphism Summary

**One-liner:** Indigo glass morphism restyling of ghost mode event cards with NSVisualEffectView blur, gradient borders, inner glow, and gradient dividers.

## What Was Done

### Task 1: Apply indigo glass morphism to EventCardNSView
**Commit:** `d6d382e`

Applied 9 specific style changes to EventCardView.swift:

1. **Color constants** -- cardBg alpha 0.55 -> 0.85, replaced `cyanAccent` with `accentColor` (#818CF8 indigo-400), added indigoStart/Mid/End palette
2. **Glass blur background** -- NSVisualEffectView with .behindWindow blending, .hudWindow material, auto-resizing mask
3. **Gradient border** -- CAGradientLayer with indigo gradient, CAShapeLayer even-odd mask for 1.5pt border effect
4. **Inner glow** -- Radial CAGradientLayer at sublayer index 0, indigo-400 at 15% alpha fading to clear
5. **layout() override** -- Updates blurView, borderLayer, glowLayer frames and re-creates border mask on resize
6. **Text colors** -- "is talking" label alpha 0.8 -> 0.7 for softer appearance
7. **Gradient dividers** -- Replaced solid white/15% dividers with horizontal gradient (clear -> white/30% -> clear)
8. **Title pill restyling** -- White text on glass background (indigo/15%) with indigo/30% border (was black text on solid cyan)
9. **Image container border** -- Added 1pt indigo/30% border to image views in TextWithImage variant

**Layout preserved:** All frame sizes (280px width), padding (14pt), corner radius (12pt), and subview hierarchy unchanged.

## Deviations from Plan

None -- plan executed exactly as written.

## Verification Results

- Swift build: **Build complete** -- compiles without errors
- `cyanAccent` references: **0** (fully replaced with `accentColor`)
- New patterns present: **16 occurrences** of glass morphism APIs (NSVisualEffectView, CAGradientLayer, behindWindow, hudWindow, indigo palette)
- Layout constants preserved: All `padding: CGFloat = 14`, `width: 280`, `cornerRadius = 12` intact

## Notes

- Full visual verification requires xcframework rebuild + app deployment (not part of this plan)
- The xcframework rebuild will be needed before the next visual checkpoint
- Glass blur effect (behindWindow) will only render properly when the overlay panel is displayed over other content
