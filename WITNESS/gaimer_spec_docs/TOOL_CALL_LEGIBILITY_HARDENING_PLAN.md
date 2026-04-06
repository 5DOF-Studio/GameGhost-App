# Tool-Call Legibility Hardening Plan

**Project:** Gaimer Desktop (.NET MAUI)  
**Date:** 2026-03-19  
**Status:** Planned  
**Scope:** Tool-call event presentation consistency across timeline and ghost mode, with verification grounded in the current routing and display wiring

---

## Summary

Tool-call visibility is implemented and functionally correct, but live verification found a presentation inconsistency: tool-use is legible in ghost mode and technically present in the timeline, yet the timeline treatment does not feel fully aligned with the rest of the event system.

The current implementation already has:

- backend-owned tool metadata (`DisplayName`, `Icon`, `ActionLabel`)
- structured routing from `BrainResult.ToolCalls`
- dedicated timeline `ToolCallTemplate`
- dedicated ghost tool-use layout

The problem is not missing plumbing. The problem is that the timeline tool-call presentation is a narrow muted inline status treatment, while ghost mode uses a more intentional icon-led card. The UI language is therefore split across surfaces in a way that reads as inconsistent rather than deliberately tiered.

This plan defines the next design/implementation slice needed to make tool calls feel intentional, readable, and consistent without turning them into normal commentary.

---

## Current Wiring

The current code path is already solid end to end.

## Metadata

Tool presentation metadata is resolved from [ToolDefinition.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Models/ToolDefinition.cs#L1):

- `DisplayName`
- `Icon`
- `ActionLabel`

`ToolCallInfo` then exposes:

- `DisplayName`
- `Icon`
- `ActionLabel`
- `SummaryText`
- `DurationLabel`

See [ToolCallInfo.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Models/ToolCallInfo.cs#L1).

## Routing

The brain attaches tool executions to `BrainResult.ToolCalls`. `BrainEventRouter` emits them first, before the final related reply:

- `BrainResult.ToolCalls`
- `BrainEventRouter.OnToolCall(...)`
- `TimelineEvent(Type=ToolCall, ToolCall=toolCall)`
- `ToolCallReceived`

See:

- [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs#L241)
- [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs#L338)

This ordering is verified by tests in [ChatTimelineRoutingTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Services/ChatTimelineRoutingTests.cs#L126).

## Timeline presentation

`TimelineEventTemplateSelector` maps `EventOutputType.ToolCall` to a dedicated template:

- [TimelineEventTemplateSelector.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Utilities/TimelineEventTemplateSelector.cs#L1)

That template currently renders as a small muted inline status row:

- 16px icon
- 14pt text
- transparent background
- thin blue stroke

See [TimelineView.xaml](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Views/TimelineView.xaml#L73).

## Ghost presentation

`MainViewModel` subscribes to `ToolCallReceived` and routes tool calls into `SlidingPanelContent` plus ghost card presentation:

- [MainViewModel.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs#L248)

Ghost mode then renders tool-use with:

- centered 40px icon
- centered action phrase
- dedicated tool-use layout path

See [GaimerHudView.xaml](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Views/GaimerHudView.xaml#L151).

This path is covered by [MainViewModelGhostModeTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/MainViewModelGhostModeTests.cs#L230).

---

## Problem Statement

### What is already correct

1. Tool calls are emitted as their own event type.
2. Tool calls appear before the final related reply.
3. Tool calls use backend-owned icon/action metadata.
4. Ghost mode does not render tool-use through the normal plain-text content path.

### What feels wrong in live use

1. The timeline tool-call row is visually smaller and flatter than surrounding event types.
2. The timeline treatment reads more like debug/status chrome than intentional product UI.
3. Ghost mode is much more icon-led and legible than the timeline, making the two surfaces feel mismatched.
4. The hierarchy between operational tool status and commentary is conceptually right, but the current timeline treatment may be under-designed rather than simply distinct.

### Key product tension

Tool-use should:

- be clearly distinguishable from commentary
- read at a glance
- feel like part of the same system
- not overpower the actual analysis/commentary output

The current implementation hits the first constraint, but not the full balance.

---

## Design Goal

Make tool-use feel like a first-class operational event type with a consistent visual language across timeline and ghost mode.

That does **not** mean making tool-use look like a normal commentary bubble.

It means:

- same semantic identity across surfaces
- intentional hierarchy
- fast legibility
- compactness without feeling vestigial

---

## Recommended Direction

## 1. Keep tool-use distinct from commentary

Do not convert tool calls into normal `DefaultEventTemplate` capsules.

The decision in [chronicles/DECISION_LOG.md](/Users/tonynlemadim/Developer/gAImer_desktop/chronicles/DECISION_LOG.md#L73) remains correct:

- tool-use is operational/status information
- it should not visually compete with actual coaching/commentary

## 2. Align the timeline treatment with the ghost semantics

Ghost mode currently uses:

- larger icon
- centered short phrase
- intentionally sparse content

The timeline should preserve its denser feed style, but adopt the same conceptual structure:

- prominent icon
- short action phrase
- optional light secondary metadata like duration

## 3. Make the timeline template feel intentionally designed, not merely smaller

The current timeline tool-call template is technically correct, but too close to a generic low-emphasis status chip.

Recommended visual shift:

- slightly stronger icon presence
- clearer spacing and alignment
- optional secondary duration text
- a card/chip treatment that feels deliberate without becoming commentary-sized

---

## Proposed Implementation

## Timeline template changes

Revise [TimelineView.xaml](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Views/TimelineView.xaml#L73) `ToolCallTemplate`.

### Expanded state

Current:

- transparent background
- thin stroke
- 16px icon
- one muted label

Recommended:

- subtle filled background or slightly stronger tinted surface
- 18-20px icon
- primary action label using `Summary`
- optional secondary duration label using `ToolCall.DurationLabel`
- more intentional padding and spacing

### Collapsed state

Current:

- tiny icon token

Recommended:

- keep compact token behavior
- slightly improve contrast/size parity with other collapsed states
- preserve tooltip/action summary

## Metadata usage

The backend metadata is already sufficient:

- `SummaryText`
- `Icon`
- `DurationLabel`

No backend contract changes should be required for the first hardening slice.

## Ghost mode

Ghost mode likely needs no structural change. It already expresses the intended semantics.

At most:

- confirm title/body/icon treatment matches the revised timeline vocabulary
- avoid diverging wording between ghost and timeline

---

## What Not To Change

1. Do not collapse tool calls back into general commentary events.
2. Do not remove backend-owned metadata resolution from `ToolDefinition` / `ToolCallInfo`.
3. Do not reorder tool-call events after the final reply.
4. Do not create a second competing ghost pipeline just for tools.

---

## Tests and Verification

## Existing coverage

Already covered:

- tool-call metadata resolution
- tool-call event routing
- tool-call ordering before final reply
- ghost `ToolCallReceived` card rendering path

See:

- [ChatTimelineRoutingTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Services/ChatTimelineRoutingTests.cs#L126)
- [BrainEventRouterTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Brain/BrainEventRouterTests.cs#L690)
- [MainViewModelGhostModeTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/MainViewModelGhostModeTests.cs#L230)
- [ToolDefinitionTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Session/ToolDefinitionTests.cs#L90)

## Missing coverage

What is not currently covered well:

1. timeline visual legibility expectations
2. duration label display expectations
3. consistency of wording/icon hierarchy between timeline and ghost paths

## Recommended test additions

### 1. Template/data contract tests

Add tests that assert:

- tool-call events carry `Summary`, `Icon`, and `ToolCall.DurationLabel` correctly
- failure states still render sensible action text

### 2. Optional UI-oriented regression checks

If this repo’s MAUI test surface supports it cleanly, add narrow tests around:

- `ToolCallTemplate` binding expectations
- visibility of duration label only when present

If not, rely on:

- viewmodel/router tests for data shape
- manual verification checklist for final presentation pass

### 3. Live verification checklist update

The live checklist should continue verifying:

- distinct tool-call event
- icon and action summary read clearly
- tool-use does not look like normal commentary

See [CLOUD_LIVE_VERIFICATION_CHECKLIST.md](/Users/tonynlemadim/Developer/gAImer_desktop/WITNESS/gaimer_spec_docs/CLOUD_LIVE_VERIFICATION_CHECKLIST.md#L66).

---

## Slice Plan

## Slice 1: Timeline tool-call template refinement

### Scope

- update `ToolCallTemplate`
- improve icon/text/duration hierarchy
- keep compact operational look

### Acceptance

- timeline tool-call events remain distinct from commentary
- presentation feels intentional and readable

## Slice 2: Wiring verification and targeted tests

### Scope

- add/adjust tests for duration and summary display data
- ensure no backend contract regression

### Acceptance

- existing routing tests still pass
- new data-shape assertions pass

## Slice 3: Manual live verification

### Scope

- run tool-call path in cloud live verification flow
- compare timeline vs ghost feel

### Acceptance

- tool-call event is clear in timeline
- ghost and timeline feel like the same event family
- tool-use remains subordinate to final commentary

---

## Acceptance Criteria

This work is complete when:

1. tool-call events are clearly distinct from commentary
2. tool-call events are immediately legible in the timeline
3. ghost mode and timeline feel visually related rather than contradictory
4. backend metadata/routing remains unchanged and correct
5. targeted tests still pass and live verification no longer flags presentation inconsistency

---

## References

- [ToolDefinition.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Models/ToolDefinition.cs#L1)
- [ToolCallInfo.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Models/ToolCallInfo.cs#L1)
- [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs#L241)
- [TimelineEventTemplateSelector.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Utilities/TimelineEventTemplateSelector.cs#L1)
- [TimelineView.xaml](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Views/TimelineView.xaml#L73)
- [MainViewModel.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs#L248)
- [GaimerHudView.xaml](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Views/GaimerHudView.xaml#L151)
- [ChatTimelineRoutingTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/Services/ChatTimelineRoutingTests.cs#L126)
- [MainViewModelGhostModeTests.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop.Tests/MainViewModelGhostModeTests.cs#L230)

