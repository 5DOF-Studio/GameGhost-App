# Sync Notes: gaimer-desktop -> GameGhost-App

## Goal
Keep `GameGhost-App` current with shared engine/features from `gaimer-desktop` without reintroducing GAIMER branding drift.

## Source and Target
- Source repo (local remote): `/Users/tonynlemadim/Documents/5DOF Projects/gAImer/gAImer_desktop`
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
