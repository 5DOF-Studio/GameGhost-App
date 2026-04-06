# Timeline UX Design — Focus Compression, Tool Visibility, Archive Boundary

**Date:** 2026-03-14
**Status:** Implemented
**Branch:** post-mini-dev

---

## 1. Focus Compression (T19)

### Problem
The timeline was visually busy. Latest assistant/commentary events rendered as truncated capsules, hiding the freshest content at the moment it mattered most.

### Design
- **Newest event** renders as a full readable bubble (expanded)
- **Older events** auto-collapse to 36x36 circular icon tokens showing only the event-type icon
- **Tap** any collapsed token to expand it; tap again to collapse
- **IsLatest guard**: the newest event cannot be manually collapsed — it always stays readable

### Data Model
```
TimelineEvent : INotifyPropertyChanged
├── IsExpanded (bool, with INPC)
├── IsCollapsed (computed: !IsExpanded)
├── IsLatest (bool — set by TimelineFeed, guards ToggleExpandedCommand)
└── ToggleExpandedCommand (ICommand — no-op when IsLatest)
```

### TimelineFeed Behavior
- `AddEvent()` → collapse previous latest (`IsExpanded=false`, `IsLatest=false`) → expand new event (`IsExpanded=true`, `IsLatest=true`)
- Manual expansion of older events is independent of latest tracking
- `Clear()` resets `_latestEvent` to null

### Template Structure
Each template (DirectMessage, ProactiveAlert, Default) contains:
- **Expanded view** — full content, visible when `IsExpanded`
- **Collapsed view** — 36x36 icon circle with tooltip, visible when `IsCollapsed`
- Both have `TapGestureRecognizer` bound to `ToggleExpandedCommand`

---

## 2. Tool-Call Visibility (T16)

### Problem
Users couldn't see when the brain/tool pipeline invoked a tool. The timeline was opaque about operational activity.

### Design
- **EventOutputType.ToolCall** — distinct event type for tool invocations
- **Muted blue visual** (`#2060A0FF` bg, `#3060A0FF` stroke) — reads as operational status, not commentary
- **Backend-owned metadata** — `ToolCallInfo.SummaryText`, `Icon`, `ActionLabel`, `DurationLabel` resolve from `ToolDefinitions`
- **No UI-side tool-name switch logic** — all presentation data comes from the model

### Data Flow
```
OpenRouterBrainService tool loop
  → collects List<ToolCallInfo> (name, duration, success)
  → attaches to BrainResult.ToolCalls
  → Channel<BrainResult>
  → BrainEventRouter.RouteBrainResult()
    → OnToolCall() per tool call
      → TimelineEvent(Type=ToolCall, ToolCall=toolCall)
      → ToolCallReceived event
  → MainViewModel
    → if ghost/FAB active: SlidingPanelContent with ToolCall
```

### Timeline Template
- **Expanded**: transparent bg, 1px blue border, 14pt muted text, 16px icon at 0.7 opacity
- **Collapsed**: 28x28 icon circle (smaller than other event types)

### Ghost Mode
- **ToolCallReceived** event on IBrainEventRouter → MainViewModel subscription
- `SlidingPanelContent.ToolCall` / `IsToolCall` / `ToolIconPath` drive layout switch
- **GaimerHudView dedicated inner layout**: centered 40x40 icon + short action phrase
- `HasTextPanelContent` separates tool cards from normal AI content
- 3-second auto-dismiss (vs 5s for normal insights)

---

## 3. Archived Boundary (T18-boundary)

### Problem
Long-running sessions need a visible end-of-scroll marker separating live events from archived history.

### Design
- **Grey capsule** — `#10808080` bg, `#20808080` 1px border, clock icon, "Archived" text
- **Checkpoint-level** — not inside EventLine 3-column grid, so truly centered in feed width
- **Global boundary** — separate `TimelineCheckpoint` at the end of `Checkpoints` collection (bottom of newest-first feed)
- **Presentation seam only** — no retention/pruning logic yet

### Data Model
```
TimelineCheckpoint
└── IsArchiveBoundary (bool) — when true, renders centered capsule instead of header+events
```

### TimelineFeed.InsertArchiveBoundary()
- Removes any existing archive boundary first (only one at a time)
- Appends new `TimelineCheckpoint { IsArchiveBoundary = true }` at end
- No EventLines or TimelineEvents created — the checkpoint IS the marker

### TimelineView.xaml
Checkpoint template has two mutually exclusive layouts:
- **Normal** (visible when `!IsArchiveBoundary`): header + event lines
- **Archive** (visible when `IsArchiveBoundary`): centered grey capsule

---

## 4. Toggle Responsiveness (T15)

### Problem
Voice chat toggle felt laggy — async work delayed visual feedback.

### Design
- **Requested state** (toggle handle) moves immediately on tap
- **Effective state** (LED) pulses while async work runs, goes solid when confirmed
- **Failure snap-back**: if `StartRecordingAsync` throws, toggle reverts to OFF

### IndustrialToggleSwitch.IsPending
- `IsPending` BindableProperty triggers LED pulse animation (opacity 0.3–0.9, 800ms cycle)
- `StopPendingAnimation()` restores LED to match current toggle state
- `AnimateToState()` skips LED update when pending (animation owns the LED)

### MainViewModel.IsVoiceChatPending
- Set to `true` at start of `HandleVoiceChatToggleAsync`
- Cleared in `finally` block (success or failure)
- Also cleared in `StopSessionAsync()` for disconnect-while-pending edge case
