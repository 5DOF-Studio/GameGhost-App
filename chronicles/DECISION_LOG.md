# Decision Log

**Project:** Gaimer Desktop

---

## 2026-02-25 — SCK Debugging & TCC Resolution

### D-012: Task.detached over @MainActor for SCK Swift dispatch
**Decision:** Use `Task.detached(priority: .userInitiated)` instead of `Task { @MainActor in ... }` for dispatching SCK capture calls from the Swift helper.
**Rationale:** Researched predecessor app (Jiga-MacOS) which used `Task.detached` on background threads. `@MainActor` can deadlock when C# blocks with `Task.Wait()` on the timer thread -- the main thread is occupied by the @MainActor task while C# is waiting for the callback, creating a circular dependency. `Task.detached` runs on a cooperative thread pool, avoiding the main thread entirely.
**Alternative rejected:** `@MainActor` dispatch -- caused intermittent hangs in the capture pipeline.

### D-013: NativeLibrary.SetDllImportResolver for framework loading
**Decision:** Add a static constructor in `NativeMethods.cs` that calls `NativeLibrary.SetDllImportResolver` to manually locate and load the `GaimerScreenCapture.xcframework` from the app bundle.
**Rationale:** .NET's default `DllImport` resolution cannot find xcframeworks embedded in a Mac Catalyst app bundle. The framework exists at `AppBundle.app/Contents/Frameworks/GaimerScreenCapture.framework/GaimerScreenCapture` but `DllImport("GaimerScreenCapture")` throws `DllNotFoundException`. The resolver probes the bundle's Frameworks directory and loads the binary directly via `NativeLibrary.Load`.
**Alternative rejected:** Relying on default DllImport search paths -- does not cover Mac Catalyst bundle layout.

### D-014: Apple Development cert + entitlements over ad-hoc signing
**Decision:** Sign the deployed app with "Apple Development: Ike Nlemadim (DCRQMPF7A9)" and an entitlements plist, instead of ad-hoc signing (`codesign --sign -`).
**Rationale:** TCC (Transparency, Consent, and Control) requires a stable code identity to persist Screen Recording permission grants. Ad-hoc signing generates a new identity per build, causing macOS to treat each build as an unrecognized app. With an Apple Development certificate, the code identity is stable -- TCC grants persist across builds.
**Trade-off:** Requires a valid Apple Development certificate on the build machine. All team members need their own cert or a shared one.

### D-015: Deploy to /Applications/ over /tmp/
**Decision:** Deploy the signed app to `/Applications/GaimerDesktop.app` instead of `/tmp/GaimerDesktop.app`.
**Rationale:** System Settings > Privacy & Security > Screen Recording showed "(null)" for apps launched from /tmp. macOS TCC uses the app's filesystem location as part of identity resolution, and /tmp is treated as a transient location. /Applications/ is a standard install location that TCC recognizes properly, allowing the app to appear by name in permission management UI.
**Alternative rejected:** /tmp/ deployment -- TCC showed "(null)" identity, making permission management impossible for users.

### D-016: Remove CGRequestScreenCaptureAccess on macOS Sequoia
**Decision:** Do not call `CGRequestScreenCaptureAccess()` on macOS Sequoia (15.x).
**Rationale:** On macOS Sequoia, `CGRequestScreenCaptureAccess()` opens System Settings on every app launch instead of showing a one-time permission dialog. This is a known Apple behavior change in Sequoia. The app should instead rely on SCK returning error -3801 and guiding the user to System Settings manually, or simply having the user pre-grant permission.
**Alternative rejected:** Calling `CGRequestScreenCaptureAccess()` -- causes System Settings to open on every launch, annoying the user.

### D-017: tccutil reset for stale TCC entries
**Decision:** Use `tccutil reset ScreenCapture com.5dof.gaimer` to clear stale TCC database entries when switching signing identities.
**Rationale:** Multiple failed signing attempts (ad-hoc, then Apple Development) left conflicting entries in the TCC database. macOS TCC stores permissions keyed by code identity -- having multiple stale entries from different identities caused permission checks to fail even with a valid grant. Resetting the TCC entries for the bundle ID forced a clean permission prompt.
**Note:** This is a development-time debugging tool, not a production solution.

---

## 2026-02-25 — Phase 03 Screen Capture

### D-005: ScreenCaptureKit via native Swift xcframework
**Decision:** Build a thin Swift library exporting C-callable functions via `@_cdecl`, packaged as an xcframework, consumed by .NET MAUI via P/Invoke.
**Rationale:** .NET 8 MacCatalyst has no managed ScreenCaptureKit bindings. Third-party binding generators (ObjCSharp, etc.) don't cover SCK. A native Swift helper with C exports is the simplest boundary -- no ObjC block marshaling, no Swift async crossing the ABI, just scalar types and a function pointer.
**Alternative rejected:** Managed bindings via ObjCSharp -- immature SCK support, heavy maintenance burden. Also rejected: shipping a separate capture daemon process.

### D-006: @_cdecl + P/Invoke over ObjC block marshaling
**Decision:** Export Swift functions with `@_cdecl` (C calling convention) and consume via `[DllImport]` P/Invoke on the C# side.
**Rationale:** `@_cdecl` produces stable C symbols that P/Invoke can call directly. The alternative -- marshaling ObjC blocks or Swift closures across the ABI -- requires complex `BlockLiteral` interop, is fragile across Swift versions, and has poor documentation for Mac Catalyst.
**Trade-off:** Limited to C-compatible parameter types (scalars, pointers, function pointers). Acceptable for this use case (window ID, dimensions, byte callback).

### D-007: SCScreenshotManager (single-frame) over SCStream (continuous)
**Decision:** Use `SCScreenshotManager.captureImage()` for each capture tick, not `SCStream` with `CMSampleBuffer` output.
**Rationale:** The app captures one frame every 30 seconds. `SCStream` is designed for continuous real-time capture (screen recording, video conferencing) and adds complexity: output handler delegates, CMSampleBuffer management, stream start/stop lifecycle. `SCScreenshotManager` is a single async call per frame -- exactly what we need.
**Trade-off:** If capture interval drops below ~1 second in the future, `SCStream` would be more efficient. For 30-second intervals, single-frame is strictly simpler.

### D-008: ImageIO for CGImage-to-PNG (not AppKit NSBitmapImageRep)
**Decision:** Use ImageIO's `CGImageDestination` to convert CGImage to PNG data.
**Rationale:** `NSBitmapImageRep` requires AppKit, which is unavailable on Mac Catalyst. ImageIO works on all Apple platforms (iOS, Catalyst, macOS). The conversion is three calls: create destination, add image, finalize.
**Alternative rejected:** `NSBitmapImageRep` -- crashes on Catalyst with "AppKit is not available on this platform."

### D-009: One-shot timer pattern for capture interval
**Decision:** Use `Timer(callback, null, 0, Timeout.Infinite)` with manual re-arm in the `finally` block, instead of a repeating timer.
**Rationale:** SCK capture is asynchronous and variable-latency (typically 100ms-2s depending on window complexity). A repeating timer at 30s interval could fire the next tick while the previous capture is still in-flight, causing overlapping P/Invoke calls and duplicate frames. One-shot guarantees sequential execution: capture completes -> wait 30s -> next capture.
**Pattern:** Fire once -> do work -> re-arm in `finally` (ensures re-arm even on exception).

### D-010: Cancel-Swap-Notify for ImageSource lifecycle
**Decision:** When updating the preview image, cancel the capture timer first, swap the ImageSource, then notify the UI via property change.
**Rationale:** MAUI's `ImageSource` is not thread-safe and holds native resources. Swapping the source while a capture callback is writing to it causes crashes or memory leaks. The Cancel-Swap-Notify sequence ensures no concurrent access: timer is stopped, old source is replaced, UI picks up new source on next layout pass.

### D-011: CapsuleColorHex persisted on TimelineEvent at creation
**Decision:** Store the capsule color as a hex string on the `TimelineEvent` model at creation time, not computed at display time.
**Rationale:** The capsule color is determined by the event type (insight, warning, move analysis, etc.) via `EventIconMap`. Computing it at display time couples the view to the icon mapping logic and makes it harder to persist/restore timeline state. Storing the hex string at creation makes events self-contained for serialization (Phase 04 persistence).

---

## 2026-02-25 — Claude Code Transition Session

### D-001: Skip Witness -> Gaimer rename
**Decision:** Defer namespace/project rename
**Rationale:** "Witness" is in namespace, solution, XAML, all file paths. Rename is cosmetic only. Owner prefers to focus on features.
**Impact:** Low. Internal naming only; no user-facing "Witness" text.

### D-002: ObservableCollection thread safety strategy
**Decision:** Encapsulate dispatch inside TimelineFeed (not callers)
**Rationale:** Multiple callers (BrainEventRouter, MainViewModel) modify timeline. Pushing thread safety into the service guarantees safety regardless of caller context. Uses `MainThread.IsMainThread` check to avoid deadlocks when already on UI thread.
**Alternative rejected:** Require all callers to dispatch -- fragile, easy to miss.

### D-003: Pending message correlation via timeout
**Decision:** 30-second timeout on `_pendingUserMessage` with `DateTime` tracking
**Rationale:** The AI may send unsolicited text (image analysis, proactive alerts) while a user message is pending. Without correlation, these get misrouted as direct replies. A timeout is simple and effective -- if the AI hasn't replied in 30s, the pending state is stale.
**Alternative rejected:** Request ID correlation -- requires API changes to all providers.

### D-004: Fire-and-forget pattern: ContinueWith over try-catch wrapper
**Decision:** Use `.ContinueWith(t => Debug.WriteLine(...), OnlyOnFaulted)` for all fire-and-forget calls
**Rationale:** Minimal code change, no wrapper method needed, consistent pattern. Debug.WriteLine captures diagnostics without crashing the app.
**Trade-off:** No recovery logic -- just logging. Acceptable for V1 where these are best-effort operations.

---

## Pre-2026-02-25 (Cursor Era)

See `.planning/STATE.md` Decisions Made section for Phase 01-02 decisions.
Key decisions carried forward:
- Singleton MainViewModel shared across views
- IConversationProvider abstraction (Gemini, OpenAI, Mock)
- Timeline hierarchy: Checkpoint -> EventLine -> TimelineEvent
- Brain Event Router pattern for routing AI output
- Agent feature gating (Leroy available, Derek/Wasp gated)
