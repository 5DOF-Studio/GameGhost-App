# GameGhost-App — Open Threads

## Active

### OT-001: Test Suite Verification
- **Status:** Not started
- **Context:** Tests were overlaid from gaimer-v2 but not yet run in GameGhost context. The branding change in ChatPromptBuilder ("Gaimer" → "Game Ghost") requires corresponding test update (done in code, needs runtime verification).
- **Next:** Run `dotnet test` and verify all 1500+ tests pass

### OT-002: Project Direction Decision
- **Status:** Open question
- **Context:** GameGhost-App currently operates as a sync fork of gaimer-desktop. With the full overlay complete, decide whether this repo should:
  - (A) Stay as a branded sync fork (periodic syncs from gaimer-v2)
  - (B) Become an independent project with its own roadmap (diverge from gaimer-desktop)
- **Next:** User decision needed

### OT-003: Missing xcframework Info.plist
- **Status:** Known — non-blocking
- **Context:** `GaimerScreenCapture.xcframework` and `GaimerSpeech.xcframework` produce build warnings about missing `Info.plist`. These frameworks need to be built locally from the NativeHelpers source packages. Same issue exists in gaimer-desktop.
- **Next:** Build xcframeworks from source when native functionality is needed

## Resolved
(none yet)
