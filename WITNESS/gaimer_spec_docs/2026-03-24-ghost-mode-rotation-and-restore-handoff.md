# Ghost Mode Rotation And Restore Handoff

Date: 2026-03-24
Owner: Codex
Review target: Claude
Scope: Ghost mode restore reliability, target-monitor placement, structured-analysis retention, ghost notification rotation, and review-ready evidence.

## Summary

This change set addresses the current live-test failures in ghost mode without folding the whole presentation stack into a broad scheduler refactor.

Delivered:

- Structured brain analysis no longer drops pending events when a new batch arrives.
- Ghost mode now gets an immediate ordered batch signal and rotates that batch locally as a notification sequence.
- Ghost tray behavior now matches the requested model more closely:
  - open once
  - show each queued message for roughly 1 second
  - replace content in place
  - collapse after the queue drains
- Ghost panel placement is resolved against the selected capture target before ghost mode is shown, instead of relying on `NSScreen.main`.
- Native host-window hide/show was hardened to reduce silent restore failures and race sensitivity.
- The mounted `GaimerGhostMode.xcframework` was rebuilt and recopied so the native changes are actually present in the app artifact.

Not delivered:

- Persistent free-position FAB storage
- A unified cross-surface priority scheduler for voice/tool/analysis/error overlays
- Explicit native success/failure status returned from host-window restore back into C#
- Live manual validation against a real fullscreen game session

## Decisions

### 1. Separate ghost rotation from the main timeline drip

Decision:

- Keep the existing `BrainEventRouter` drip-fed emission loop for timeline/main-view behavior.
- Add a second immediate signal for ghost mode: `AnalysisBatchQueued`.

Why:

- The main view and ghost mode need different pacing semantics.
- Reusing the 2.5s timeline drip for ghost mode is the direct cause of the "only one card surfaces before the next batch clears it" problem.
- A dedicated ghost batch signal avoids regressing the timeline while allowing ghost mode to surface all messages quickly.

Files:

- `src/WitnessDesktop/WitnessDesktop/Services/IBrainEventRouter.cs`
- `src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs`

Alternatives considered:

- Replace the existing emission loop entirely with a faster global cadence.
  Rejected because it would change main-view behavior and still would not give proper "single tray, rotating content" semantics.
- Push a full queue into native Swift.
  Rejected for now because the native card already supports in-place replacement; managed scheduling was enough for the requested behavior with less interop churn.

### 2. Stop destructive queue clearing in `BrainEventRouter`

Decision:

- Remove `_emissionQueue.Clear()` from `OnStructuredAnalysis`.

Why:

- That line was the direct loss point for pending `Danger`, `Assessment`, and `SageAdvice` events.
- Preserving arrival order is more correct than silently dropping previously enqueued analysis.

Tradeoff:

- Timeline backlog can grow if brain results outpace the drip loop.
- This is acceptable in the short term because the top strip still refreshes to latest immediately, and the user explicitly wanted ghost mode to surface all queued messages instead of truncating them.

Files:

- `src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs`
- `src/WitnessDesktop/WitnessDesktop.Tests/Brain/BrainEventRouterTests.cs`

### 3. Implement ghost notification rotation in C#, not Swift

Decision:

- Add a managed ghost notification queue inside `MainViewModel`.
- Use repeated `_ghostModeService.ShowCard(...)` calls to replace card content in place.

Why:

- The native unified card already supports in-place replacement without requiring collapse/reopen between messages.
- Keeping this queue in managed code made it easy to:
  - subscribe to `AnalysisBatchQueued`
  - queue tool cards
  - interrupt on alerts
  - keep main-view logic unchanged

Behavior:

- Analysis batch items are queued at 1.0s each.
- Tool-call cards are queued at 1.2s each.
- Errors interrupt the queue and show as sticky alerts.
- Voice VAD card display is suppressed while a ghost notification sequence is active to avoid fighting over the same card surface.

Files:

- `src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs`
- `src/WitnessDesktop/WitnessDesktop.Tests/MainViewModelGhostModeTests.cs`

Alternatives considered:

- Build native queueing and timer logic in Swift.
  Rejected for now because the complexity was unnecessary for this fix set, and it would make review/debugging harder across the interop boundary.

### 4. Resolve panel placement before showing ghost mode

Decision:

- Remove native `ghost_panel_show()` auto-repositioning to `NSScreen.main`.
- Pre-position and resize the panel from C# based on the selected target screen before calling `EnterGhostModeAsync()`.

Why:

- The existing implementation guaranteed wrong initial placement on multi-monitor setups because `ghost_panel_show()` always snapped to the primary screen.
- The existing managed method `PositionGhostPanelOnTargetScreen()` existed but was never called.

Important implementation detail:

- The old managed positioning math assumed a small panel.
- The actual native ghost panel is a 420pt-wide strip spanning the target screen’s visible height.
- The fix now sets:
  - width = `420`
  - height = target screen visible height
  - x = right edge of target screen minus panel width
  - y = target screen visible minY

Files:

- `src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs`
- `src/WitnessDesktop/WitnessDesktop/Services/IGhostModeService.cs`
- `src/WitnessDesktop/WitnessDesktop/Services/MockGhostModeService.cs`
- `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/MacGhostModeService.cs`
- `src/WitnessDesktop/NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/GaimerGhostMode.swift`

### 5. Harden host-window hide/show by removing async race sensitivity

Decision:

- Switch critical native panel/window operations from `DispatchQueue.main.async` to `runOnMainSync(...)` where ordering matters:
  - `ghost_panel_show`
  - `ghost_panel_hide`
  - `ghost_panel_set_position`
  - `ghost_panel_set_size`
  - `ghost_panel_hide_host_window`
  - `ghost_panel_show_host_window`

Why:

- The original code returned control to C# before the main-thread work had actually run.
- That made enter/exit ordering fragile and increased the chance of:
  - panel show before placement
  - restore timing races
  - host-window capture/restore becoming harder to reason about

Additional hardening:

- Store the hidden host window reference and `windowNumber`.
- Resolve host windows by preferring `UINSWindow` candidates and key/main/visible windows.
- Use `orderFrontRegardless()` plus `makeKey()` on restore.

Files:

- `src/WitnessDesktop/NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/GaimerGhostMode.swift`

Alternatives considered:

- Add a native restore-success return value over P/Invoke and handle failure explicitly in C#.
  Deferred. It is a good next step, but not required to ship this fix set.

### 6. Clarify session trace path instead of guessing `/tmp`

Decision:

- Keep current session trace service behavior but log the actual trace file path when a run starts.

Why:

- The current service writes to `FileSystem.AppDataDirectory/traces`, not `/tmp/gaimer-traces`.
- The live-test diagnosis looked at the wrong path.
- Logging the concrete path makes the next live test easier to inspect without changing app storage policy in this patch.

Files:

- `src/WitnessDesktop/WitnessDesktop/Services/SessionTraceService.cs`

## Progress

### Completed code changes

Managed:

- Added `AnalysisBatchQueued` event to `IBrainEventRouter`.
- `BrainEventRouter.OnStructuredAnalysis(...)` now:
  - emits `AnalysisBatchQueued`
  - preserves pending emission queue entries
- Added `SetSize(...)` to `IGhostModeService` and implementations.
- `MainViewModel` now:
  - enqueues ghost notification batches immediately
  - rotates ghost batch items on a dedicated managed loop
  - queues tool-call cards instead of forcing immediate replacement
  - interrupts queue for alerts
  - suppresses voice card display while queued ghost notifications are active
  - repositions ghost panel when target changes
  - repositions and resizes panel before entering ghost mode
  - stops ghost notification loop on ghost exit and disconnect

Native:

- `ghost_panel_show()` no longer repositions to `NSScreen.main`.
- Critical panel/window operations now run synchronously on the main thread via `runOnMainSync`.
- Host window capture/restore now uses stronger `UINSWindow`-focused resolution and stored window identity.
- Rebuilt and recopied `GaimerGhostMode.xcframework`.

### Verification

Managed tests:

- Command:
  - `dotnet test src/WitnessDesktop/WitnessDesktop.Tests/WitnessDesktop.Tests.csproj -f net8.0`
- Result:
  - Passed: `1112`
  - Skipped: `12`
  - Failed: `0`

Focused tests added/updated:

- `BrainEventRouter_EmissionQueueTests.NewBatch_PreservesPendingQueue`
- `BrainEventRouter_EmissionQueueTests.RapidTripleBatch_PreservesArrivalOrder`
- `BrainEventRouter_EmissionQueueTests.OnStructuredAnalysis_FiresAnalysisBatchQueued_InPriorityOrder`
- `MainViewModelGhostModeTests.ToolCallReceived_GhostActive_UsesTextWithImageCard`
- `MainViewModelGhostModeTests.AnalysisBatchQueued_GhostActive_RotatesFullBatch`

Mac Catalyst build:

- Command:
  - `dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false`
- Result:
  - Succeeded
  - Existing warnings remain, including the pre-existing `GaimerSpeech.xcframework/Info.plist` warning

Native framework rebuild:

- Command:
  - `./build-xcframework.sh`
- Result:
  - Succeeded
  - Framework copied to:
    - `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/GaimerGhostMode.xcframework`
  - Verified retagged binary platform:
    - `MACCATALYST`

## Artifacts Changed

- `src/WitnessDesktop/WitnessDesktop/Services/IBrainEventRouter.cs`
- `src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs`
- `src/WitnessDesktop/WitnessDesktop/Services/IGhostModeService.cs`
- `src/WitnessDesktop/WitnessDesktop/Services/MockGhostModeService.cs`
- `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/MacGhostModeService.cs`
- `src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs`
- `src/WitnessDesktop/WitnessDesktop/Services/SessionTraceService.cs`
- `src/WitnessDesktop/NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/GaimerGhostMode.swift`
- `src/WitnessDesktop/WitnessDesktop.Tests/Brain/BrainEventRouterTests.cs`
- `src/WitnessDesktop/WitnessDesktop.Tests/MainViewModelGhostModeTests.cs`
- `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/GaimerGhostMode.xcframework/...`

## Open Threads

### 1. Live validation of host-window restore after background/foreground transition

Priority: High
Owner: Platform / QA

Status:

- Source changes are in place, but this still needs a real fullscreen/manual live test.

What to verify:

- Enter ghost mode while connected.
- Background the app or force a focus transition similar to the 15:34:35 scenario from the live log.
- Tap the ghost FAB and verify the MAUI host window returns reliably.

### 2. Drag behavior is still only partially productized

Priority: High
Owner: Platform / UX

Status:

- Drag logic exists in native `FabView` and is wired into `GhostPanelContentView`.
- Persistence is not implemented.
- No explicit drag telemetry exists.
- If the live app still appears undraggable, confirm the rebuilt xcframework is the one being loaded by the deployed app.

Follow-up:

- Add per-screen or per-target position persistence.
- Add telemetry or debug logging on drag start/end.

### 3. Ghost notification scheduler is intentionally scoped, not global

Priority: Medium
Owner: Brain / UX

Status:

- The new queue currently handles analysis batches and tool cards cleanly.
- Voice cards and system alerts still use separate behavior.

Risk:

- As more ghost surfaces compete for the same card, the app may need a unified scheduler with:
  - priorities
  - interruption rules
  - coalescing
  - stale-message expiry

### 4. Timeline backlog can now grow instead of being truncated

Priority: Medium
Owner: Brain

Status:

- This is an intentional tradeoff.

Risk:

- If the brain runs substantially faster than the drip loop, old queued timeline items may take longer to appear.

Recommended next step:

- Add a bounded freshness policy for the timeline emission queue that is explicit and reviewable instead of destructive and silent.

### 5. Session trace visibility improved, but path policy remains unchanged

Priority: Medium
Owner: Observability

Status:

- We now log the actual trace file path.
- The app still writes traces under app data, not `/tmp`.

Recommended next step:

- Document the canonical trace path in the live verification checklist or expose it in-app/dev diagnostics.

## Technical Debt

### Ghost/native interop still lacks explicit restore acknowledgements

Impact:

- C# assumes restore succeeded if the native function returns.
- Failures are easier to miss than they should be.

Recommended fix path:

- Add native return values or callback-based acknowledgements for:
  - host window found
  - host window hidden
  - host window restored
  - fallback used

### Main-thread synchronous native calls need manual review for deadlock safety

Impact:

- `runOnMainSync` is safer than raw `DispatchQueue.main.sync`, but these calls should still be reviewed against all known call sites.

Recommended fix path:

- Audit each C# call path to confirm they do not synchronously block the MAUI main thread in a way that would self-deadlock.
- Current evidence suggests this is safe because `runOnMainSync` short-circuits when already on main.

### The rebuilt xcframework alters packaged framework contents

Impact:

- The new framework copy removed/replaced module/signature/resource artifacts relative to the prior mounted binary.
- This is expected from the rebuild/copy flow, but reviewers should inspect the binary diff with that context.

Recommended fix path:

- Treat the xcframework as a generated artifact and review the source Swift changes first.
- If repository policy allows, consider formalizing native helper build outputs and artifact expectations.

## Reviewer Notes For Claude

Primary review questions:

1. Is preserving timeline queue order the right short-term behavior, or should we impose a freshness cap immediately?
2. Is the managed ghost notification scheduler the right place for this rotation logic, or is there a strong reason to move batch sequencing native?
3. Are the new synchronous native calls acceptable from a deadlock/race perspective across the current Catalyst call graph?
4. Should danger items in ghost-mode batch rotation remain non-sticky, or should danger become an interrupt class only when raised as a standalone alert?

Areas to inspect first:

- `src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs`
- `src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs`
- `src/WitnessDesktop/NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/GaimerGhostMode.swift`

Expected behavioral deltas:

- Multi-monitor ghost placement should now follow the selected capture target rather than the primary display.
- Ghost analysis batches should show all items in order instead of collapsing to the latest batch.
- Tool cards in ghost mode should queue instead of instantly stomping over an in-progress analysis batch.
- Alert/system-error cards should still interrupt and remain sticky.

## Next Session Start

1. Run a real Mac live session with the rebuilt `GaimerGhostMode.xcframework`.
2. Validate monitor targeting using a game window on a non-primary display.
3. Validate ghost exit after background/foreground transitions.
4. Validate batch rotation with a brain result that produces at least `Danger + Assessment + SuggestedAction`.
5. Decide whether FAB drag persistence stays in this rotation or branches into a separate UX task.
6. If restore is still flaky, add explicit native restore result reporting back to C# before broadening scope.
7. If message backlog becomes noisy, define a bounded queue freshness policy rather than reintroducing destructive clears.

## PM Risk Callouts

- The source fix is shipped into the mounted xcframework, but live behavior still depends on testing the deployed app bundle, not only the project build output.
- Multi-monitor and background/foreground restore remain the two highest-risk manual validation points.
- The ghost rotation logic is intentionally tactical; if product wants a unified notification model across voice/tool/analysis/error surfaces, that should be treated as a distinct follow-on design task.
