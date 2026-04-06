# GameGhost-App — Decision Log

## D-001: Full Overlay Sync Replaces Cherry-Pick Model (2026-04-06)
- **Decision:** Replace weekly cherry-pick sync with full source overlay from gaimer-v2
- **Context:** gaimer-v2 had diverged by ~420 commits over 5 weeks (phases 1-17, deep audit, game skill packs, replay, audio intelligence). Cherry-picking was impractical.
- **Alternatives considered:**
  - (A) Batch cherry-pick: too many conflicts at 420+ commits
  - (B) Git merge/rebase: repos don't share history, would produce massive conflicts
  - (C) Abandon fork model: not chosen — two-repo model preserves independent product identity
  - **(D) Fresh overlay: chosen** — copy full gaimer-v2 source, re-apply branding
- **Rationale:** Cleanest path to sync. Branding layer is thin. Engine is identical.
- **Outcome:** 585 files synced, build verified (0 errors), pushed to main.

## D-002: Preserve Native Framework Names (2026-04-06)
- **Decision:** Keep `GaimerGhostMode`, `GaimerScreenCapture`, `GaimerSpeech` framework identifiers unchanged in GameGhost-App
- **Context:** These are xcframework/dylib names used in P/Invoke, NativeReference, and runtime loading. Renaming would require rebuilding all native frameworks and updating all interop signatures.
- **Rationale:** High effort, high risk, zero user-facing benefit. Names are never shown to users.
- **Outcome:** Documented as intentional divergence in SYNC_NOTES.md and STATE.md.
