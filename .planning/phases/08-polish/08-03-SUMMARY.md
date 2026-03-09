# Phase 08 Plan 03: Voice Chat State Machine Redesign Summary

Decoupled connection from voice chat: connect establishes text-only API session, IsVoiceChatActive toggle independently controls mic (requires connection).

## Tasks Completed

| # | Task | Commit | Key Changes |
|---|------|--------|-------------|
| 1 | Refactor MainViewModel state machine | 5236db8 | Renamed IsMicActive -> IsVoiceChatActive, removed auto-mic from ConnectionState handler, removed StartRecordingAsync from ToggleConnectionAsync, new HandleVoiceChatToggleAsync |
| 2 | Update tests for decoupled state | 6364b60 | Updated 3 existing tests, added 5 new tests verifying decoupled behavior, 493/493 passing |

## Key Changes

### State Machine Before
- IsMicActive toggle ON -> auto-connect if disconnected -> ConnectionState handler forces IsMicActive=true -> StartRecordingAsync in ToggleConnectionAsync
- IsMicActive toggle OFF -> disconnect

### State Machine After
- Connect: establishes text-only API session (no mic)
- IsVoiceChatActive toggle ON (requires connection): starts mic recording via HandleVoiceChatToggleAsync
- IsVoiceChatActive toggle OFF: stops mic recording, keeps connection alive
- Disconnect (StopSessionAsync): resets IsVoiceChatActive to false

### Files Modified
- `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs` — core state machine refactor
- `src/GaimerDesktop/GaimerDesktop/MainPage.xaml` — binding updated to IsVoiceChatActive
- `src/GaimerDesktop/GaimerDesktop/Views/AudioControlPanelView.xaml` — binding + label updated (MIC -> VOICE CHAT)
- `src/GaimerDesktop/GaimerDesktop.Tests/ViewModels/MainViewModelTests.cs` — test updates + 5 new tests

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Voice chat snaps back OFF if not connected | Prevents invalid state; user must connect first |
| StopSessionAsync resets voice chat with suppress guard | Prevents re-entrant toggle handler during cleanup |
| Ghost mode audio toggle index 0 maps to IsVoiceChatActive | Maintains existing ghost mode interop contract |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] AudioControlPanelView label update**
- **Found during:** Task 1, Step 7 (search for remaining IsMicActive)
- **Issue:** AudioControlPanelView.xaml also had IsMicActive binding and MIC label
- **Fix:** Updated binding to IsVoiceChatActive and label to VOICE CHAT
- **Files modified:** `src/GaimerDesktop/GaimerDesktop/Views/AudioControlPanelView.xaml`

## Test Results
- Total: 493/493 passing (+5 new)
- New tests verify: snap-back guard, no auto-record on connect, voice starts on toggle, voice stops without disconnect, disconnect resets voice

## Verification
- Zero `IsMicActive` references in entire project
- `StartRecordingAsync` only in HandleVoiceChatToggleAsync, not ToggleConnectionAsync
- UI shows "VOICE CHAT" label bound to IsVoiceChatActive
- MacCatalyst build clean (0 errors, 12 pre-existing warnings)
