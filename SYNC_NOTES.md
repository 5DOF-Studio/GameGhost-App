# Sync Notes: gaimer-desktop -> GameGhost-App

## Goal
Keep `GameGhost-App` current with shared engine/features from `gaimer-desktop` without reintroducing GAIMER branding drift.

## Source and Target
- Source repo (local remote): `/Users/tonynlemadim/Developer/gAImer_desktop`
- Target repo: `/Users/tonynlemadim/Documents/5DOF Projects/GameGhost-App`

## Quick Workflow
1. Ensure GameGhost working tree is clean.
2. Discover unsynced GAIMER commits:
   - `./scripts/sync_from_gaimer.sh`
3. Cherry-pick selected commit(s):
   - `./scripts/sync_from_gaimer.sh <sha1> [sha2 ...]`
4. Resolve conflicts if any, then run build/tests.
5. Commit any follow-up adjustments and push.

## Guardrails
- Prefer small, topical sync batches.
- Use `git cherry-pick -x` provenance (built into the script).
- Do not sync GAIMER-specific marketing/brand text blindly.
- After each sync, verify:
  - app name/title remains `Game Ghost`
  - `ApplicationId` remains `com.5dof.gameghost`
  - palette direction remains rich black / pale azure / yellow

## Intentional Divergences (Current)
- User-facing branding: Game Ghost.
- Some internal names remain `WitnessDesktop`.
- Native framework identifiers still include `Gaimer*` for runtime interop stability.

## Suggested Sync Cadence
- Weekly for core shared development.
- Immediately for security/stability fixes.
- Milestone-based for larger architectural changes.

## Current Handoff (2026-04-09)
- Previous baseline: `40163ce` on `gaimer-v2` (2026-04-07)
- New baseline: `643b680` on `gaimer-v2` (incremental overlay sync)
- Sync method: Targeted overlay of 31 new commits (30 source files, +9,414 lines)
- New features synced: Team Phase C+D (settings, ConnectorCard, pre-flight, context population), Team Phase G (permission request UI, event wiring, countdown/timeout), Settings sidebar-nav migration (Vision/Voice/Brain/Team tabs), BrainPromptBuilder pack-driven constructor fix, voice deferral ack fix, BrainContextFormatter
- Branding pass applied via `scripts/apply_branding.sh`: Game Ghost Dashboard, Ghost Team references, Settings sidebar
- Internal service/interface names (`IGaimerTeamService`, `GaimerPipeClient`, etc.) preserved as internal identifiers
- Resume overlay from `643b680` for future syncs

## Previous Handoff (2026-04-07)
- Baseline: `40163ce` on `gaimer-v2`
- Synced: Gaimer Team (Claude CLI integration), voice sprint, telemetry (+28 trace events), GaimerSpeech xcframework, native build script
