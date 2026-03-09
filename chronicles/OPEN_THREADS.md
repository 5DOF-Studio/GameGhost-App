# Open Threads

**Updated:** 2026-03-02

---

## Active

### T1: Deferred Code Review Findings
**Status:** Triaged, assigned to future phases
**Context:** Dual review (architecture + platform) on 2026-02-25. 8 critical/high items fixed. Remaining deferred:

#### Phase 03 (Screen Capture) -- now complete
- [x] **H1** -- Add `Disconnecting` state to ConnectionState enum
- [x] **H5** -- Thread-guard `StopSessionAsync`
- [x] **M4** -- CancellationToken in audio recording callback

#### Phase 04 (Persistence/Polish)
- [ ] **H3** -- `IAsyncDisposable` for conversation providers (WebSocket close handshake is async)
- [ ] **H4** -- `WaveOutEvent` dispose-on-stop for Windows audio recovery
- [ ] **M2** -- SessionManager non-atomic state mutation (lock or Interlocked)
- [ ] **M3** -- Magic strings for game type detection -> constants or agent metadata
- [ ] **M5** -- `SendContextualUpdateAsync` sends as user text, should be system-level context

### T2: Codesign Resource Fork Issue
**Status:** Workaround updated
**Context:** `dotnet build` on macOS fails at codesign step with "resource fork, Finder information, or similar detritus not allowed". Compilation succeeds; codesign fails.
**Current workaround:** Use `-p:EnableCodeSigning=false` for build, then `ditto --norsrc` to `/Applications/GaimerDesktop.app` + `codesign` with Apple Development cert "Apple Development: Ike Nlemadim (DCRQMPF7A9)" and entitlements plist. Documented in CLAUDE.md and HANDOFF.md.
**Root cause:** Likely Finder/Spotlight indexing the build output. May need `.gitattributes` or build output outside indexed path.

### T3: Witness -> Gaimer Rename
**Status:** Deferred by owner
**Context:** "Witness" branding is deeply embedded (namespace, project name, solution file, all XAML). Owner decided to skip rename for now. Cosmetic only -- no functional impact.

### T4: Progressive Capsule Compression Animation
**Status:** Deferred to Polish phase
**Context:** Timeline capsule events should progressively compress (shrink) as they age, creating a visual timeline density gradient. UI concept identified during Phase 03 capsule work. Non-functional -- purely visual enhancement.
**Next step:** Design the compression curve and implement in the capsule DataTemplate.

### T5: Capsule Collapse/Expand + Tooltip Removal
**Status:** Planned for Polish phase
**Context:** When >1 event of the same type occurs in a timeline checkpoint EventLine, collapse extras to icon-only mini capsules. Expand on hover (PointerGestureRecognizer on Mac Catalyst). Remove the existing `ToolTipProperties.Text` on capsules (currently shows redundant tooltip of full content). This creates a cleaner timeline and enables the "expand for detail" interaction pattern.
**Files:** `Views/TimelineView.xaml` (DefaultEventTemplate), possibly a new `CollapsedCapsuleTemplate`.

### T6: Settings Hamburger Icon
**Status:** Built but hidden
**Context:** Three-line hamburger icon already built in MainPage.xaml top bar column 2 (`Opacity="0"`, `InputTransparent="True"`). Make visible when settings page is implemented. No action needed until settings feature is ready.

---

## Resolved This Session (2026-02-25 -- SCK Debugging)

- [x] **T5: Ad-hoc Signing Causes Permission Dialog Per Build** -- RESOLVED. Switched to Apple Development certificate "Apple Development: Ike Nlemadim (DCRQMPF7A9)" with entitlements plist. Stable code identity preserves TCC grants across builds. See D-014.
- [x] **SCK TCC permission error -3801** -- RESOLVED. Root cause: ad-hoc signing + /tmp deployment + stale TCC entries. Fix: Apple Dev cert + /Applications/ deploy + tccutil reset. See D-014, D-015, D-017.
- [x] **DllNotFoundException for GaimerScreenCapture** -- RESOLVED. DllImport couldn't find xcframework in app bundle. Fix: NativeLibrary.SetDllImportResolver in NativeMethods.cs static constructor. See D-013.
- [x] **@MainActor deadlock risk in Swift helper** -- RESOLVED. Switched to Task.detached(priority: .userInitiated) based on Jiga-MacOS research. See D-012.
- [x] **Memory leaks in capture pipeline (~45MB/tick)** -- RESOLVED. UIImage, NSData never disposed. Added using statements, pre-allocated static NSString keys, fixed NSArray/NSDictionary disposal.
- [x] **Icon routing for unsolicited in-game text** -- RESOLVED. Was routing to OnImageAnalysis (video_reel) instead of OnGeneralChat (commentary_icon). Fixed in MainViewModel.cs.
- [x] **CGRequestScreenCaptureAccess on Sequoia** -- RESOLVED by removal. Opens System Settings every launch instead of one-time dialog. See D-016.

---

## Previously Resolved

- [x] CGDisplayCreateImage cannot capture GPU/Metal content -> Resolved by ScreenCaptureKit migration via native Swift xcframework
- [x] Preview memory leak from ImageSource lifecycle -> Resolved by Cancel-Swap-Notify pattern (cancel timer, swap source, notify UI)
- [x] Agentic office docs out of sync with repo -> Copied 4 docs
- [x] CLAUDE.md had wrong macOS run command -> Fixed with ditto workaround
- [x] 8 code review findings (3 critical + 5 fix-now) -> All applied and build-verified
