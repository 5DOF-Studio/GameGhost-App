# GameGhost-App — Project State

## Current Position
- **Phase:** Post-sync — gaimer-v2 overlay complete, build verified
- **Branch:** `main` (pushed to origin)
- **Last Activity:** 2026-04-06
- **Baseline:** gaimer-v2 @ `5992eea` (full overlay sync)

## Project Identity
- **Product Name:** Game Ghost
- **Bundle ID:** `com.5dof.gameghost`
- **Engine Source:** gaimer-desktop (synced via overlay from gaimer-v2 branch)
- **Repo:** `5DOF-Studio/GameGhost-App`

## Architecture
Same as gaimer-desktop (WitnessDesktop namespace):
- .NET MAUI (net8.0-maccatalyst primary target)
- Brain pipeline (cloud/local/mock/demo providers)
- Voice agents (OpenAI Realtime, Gemini, local)
- Screen capture + frame diff (SkiaSharp)
- Game Skill Packs (chess, generic)
- Replay recording
- Audio intelligence + exchange protocol
- GhostFab native overlay (AppKit via xcframework)
- Session trace telemetry
- EF Core persistence (SQLite)

## Branding Divergences from gaimer-desktop
| Element | gaimer-desktop | GameGhost-App |
|---------|---------------|---------------|
| ApplicationTitle | Gaimer | Game Ghost |
| ApplicationId | com.5dof.gaimer | com.5dof.gameghost |
| AssemblyName | Gaimer | GameGhost |
| UI Titles | Gaimer Dashboard, etc. | Game Ghost Dashboard, etc. |
| Prompt Identity | "AI copilot built into Gaimer" | "AI copilot built into Game Ghost" |
| Native framework names | GaimerGhostMode, etc. | GaimerGhostMode, etc. (preserved for interop) |
| Debug log prefixes | [Gaimer] | [Gaimer] (preserved for log compat) |
| Namespace | WitnessDesktop | WitnessDesktop (unchanged) |

## Build
```bash
dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false
```
- Last build: 2026-04-06 — **0 errors**, 22 warnings (pre-existing)

## Test Count
- Tests project: `src/WitnessDesktop/WitnessDesktop.Tests/`
- Count: inherited from gaimer-v2 (~1500+ tests, exact count TBD for this repo)

## Sync Model
- Source: `/Users/tonynlemadim/Developer/gAImer_desktop` (gaimer remote)
- Method: Full overlay (replaced cherry-pick model on 2026-04-06)
- Baseline: `5992eea` on `gaimer-v2`
- Future syncs: Resume cherry-picking from this baseline
- See: `SYNC_NOTES.md` for workflow details

## Known Issues
- GaimerScreenCapture.xcframework and GaimerSpeech.xcframework missing Info.plist (build warnings, non-blocking)
- 22 pre-existing warnings (SKFilterQuality obsolete, unused events, MVVMTK0034)

## Immediate Next
- Run tests to verify test suite works in GameGhost context
- Decide if this repo needs its own GSD roadmap or stays as a sync fork
- Consider establishing CI for build verification on push

## Session Continuity
- **Last session:** 2026-04-06
- **Stopping point:** Full overlay sync complete, build verified, pushed to main
- **Next session start:** Read `chronicles/HANDOFF.md`
