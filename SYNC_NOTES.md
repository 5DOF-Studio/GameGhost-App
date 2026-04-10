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

## Current Handoff (2026-04-09c)
- Previous baseline: `9b8c1e0` on `gaimer-v2` (2026-04-09)
- New baseline: `0cb84a2` on `gaimer-v2` (incremental overlay sync)
- Sync method: Targeted overlay of 6 new commits (13 source files)
- New features synced: Team surface routing (Claude controls voice vs timeline rendering), voice truncation helper, TeamProgress timeline events, design decision annotations, channel plugin dist rebuild
- Resume overlay from `0cb84a2` for future syncs

## Previous Handoff (2026-04-09b)
- Sync method: Targeted overlay of 35 new commits (59 source files, +7,621 / -1,300 lines)
- New features synced: Timeline redesign (flat list replacing checkpoint/bucket architecture), media cards (ImageCard/VideoCard), show_replay tool (timestamp resolution, surface routing), Ghost FAB video (SpineCardView with AVPlayerLayer), replay cleanup (24h retention sweep), codebase audit fixes (1723 tests)
- Deleted files: EventLine.cs, TimelineCheckpoint.cs (timeline refactor)
- Branding pass applied via `scripts/apply_branding.sh` (3 replacements + 1 manual fix in ToolDefinition.cs, script updated)
- Resume overlay from `9b8c1e0` for future syncs

## Previous Handoff (2026-04-07)
- Baseline: `40163ce` on `gaimer-v2`
- Synced: Gaimer Team (Claude CLI integration), voice sprint, telemetry (+28 trace events), GaimerSpeech xcframework, native build script
