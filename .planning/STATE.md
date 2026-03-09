# Project State

## Current Position
Phase: Deployment (post-v1.0.0-beta)
Plan: macOS distribution — notarized build
Status: macOS Release Build Notarized and Ready for Distribution
Last activity: 2026-03-09 - Repo moved to ~/Developer (iCloud fix), Release build notarized by Apple, 39MB distributable zip ready

Progress: ███████████████████████████████████ 100% (39/39 plans complete: phases 01-06 + 08-11 done)

## Phase Overview
| Phase | Name | Status | Plans |
| --- | --- | --- | --- |
| 01 | MainView V2 UI | Complete | 1/1 |
| 02 | Chat Brain Architecture | Complete | 6/6 |
| 03 | Screen Capture | Complete | 3/3 |
| 04 | Ghost Mode | Complete | 3/3 |
| 05 | Brain Infrastructure | Complete | 4/4 |
| 06 | Capture Pipeline | Complete | 3/3 |
| 07 | Persistence Layer | Deferred | 0/? |
| 08 | Polish | Complete | 5/5 |
| 12 | Audio Intelligence Pipeline | Research Required | 0/? |
| 09 | Stockfish Chess Engine | Complete | 4/4 |
| 10 | Integration & Orchestration Test Coverage | Complete | 4/4 |
| 11 | Agent Personality System | Complete | 6/6 |

## Phase 08 Plans
| # | Plan | Wave | Status | Description |
| --- | --- | --- | --- | --- |
| 01 | Provider State Machine Fix | 1 | Complete | Disconnecting state, local State property, event sync |
| 02 | Ghost Mode Visual Polish | 1 | Complete | Glass blur, indigo accent, card animations |
| 03 | Voice Chat State Machine Redesign | 2 | Complete | Decoupled voice/connection state, suppress guard |
| 04 | Capture Pipeline Parameterization | 3 | Complete | CaptureConfig record, per-agent pipeline config |
| 05 | Settings Page + Navigation | 3 | Complete | Bento grid settings, error page, audio guards, app icon |

## Agent Availability
| Agent | Status | Reason |
| --- | --- | --- |
| Leroy (Chess) | Available | Full personality, dual chess tools + Stockfish engine |
| Wasp (Chess Master) | Available | Full personality, dual chess tools + Stockfish engine |
| Derek (RPG) | Gated | RPG events/tools pending, no personality sections yet |

## Test Summary
- Total tests: 519/519 passing
- CI: GitHub Actions green on develop (dotnet-test.yml)
- SkiaSharp.NativeAssets.Linux.NoDependencies added to test csproj for CI
- Both ViewModels (MainViewModel + AgentSelectionViewModel) compile under net8.0
- Builds: net8.0 (library) + net8.0-maccatalyst (platform) both clean

## Decisions Made
| Decision | Plan | Rationale |
| --- | --- | --- |
| ToolGuidanceBlock as separate property | 11-01 | ChessToolGuidance is operational, not personality. Appended after personality sections in ComposedPersonality. |
| Voice providers use ComposedPersonality | 11-06 | Replaces SystemInstruction in GeminiLiveService + OpenAIRealtimeService to deliver full structured personality |
| ChatPromptBuilder uses SOUL + BEHAVIOR only | 11-02 | Text chat doesn't need voice STYLE/SITUATIONS/ANTI-PATTERNS. Simpler, focused prompt. |
| Brain uses BrainPersonalityPrefix via AgentKey | 11-03 | Compact ~200 token prefix. Brain reads from SessionContext.AgentKey -> Agents.GetByKey. |
| Wasp composed without questionnaire | 11-05 | Owner answered 64 Leroy questions. Wasp composed as distinct contrast (queen archetype, positional, measured). |
| Compile-time personality composition via const strings | 11-01 | Same pattern as ChessToolGuidance; no runtime file loading needed |
| ToolExecutor stays personality-free | 11-03 | Tools produce objective data; personality applied by brain in synthesis |
| .hudWindow + .behindWindow glass blur | 08-02 | Dark translucent glass matching sci-fi aesthetic for ghost mode cards |
| Accent color shift cyan to indigo | 08-02 | Indigo gradient creates cohesive glass morphism; cyan was flat |
| Local State property with event sync in providers | 08-01 | Enables explicit Disconnecting state before inner service call |
| Voice chat snaps back OFF if not connected | 08-03 | Prevents invalid state; user must connect first for voice |
| StopSessionAsync resets voice chat with suppress guard | 08-03 | Prevents re-entrant toggle handler during cleanup |
| CaptureConfig defaults = chess values | 08-04 | Zero behavioral change for existing agents; new agents override |
| DiffThreshold per-call override | 08-04 | FrameDiffService is DI singleton; threshold varies per agent selection |

## Deployment Status
- **macOS**: Notarized and stapled by Apple (submission c773e4ea). Distribution zip: 39MB. Ready to host on website.
- **Windows**: Not yet built. Ghost Mode Windows impl pending. Needs Windows machine or CI.
- **Repo location**: Moved from `~/Documents/5DOF Projects/gAImer/gAImer_desktop` to `~/Developer/gAImer_desktop` (iCloud File Provider fix)
- **Native xcframeworks**: Not in git — copied from old repo location. Must be rebuilt or committed for fresh clones.

## Session Continuity
Last session: 2026-03-09
Stopped at: macOS Release build notarized by Apple. Distributable zip ready at /tmp/gaimer-dist/GaimerDesktop-notarized.zip (39MB). Repo moved to ~/Developer/ to fix iCloud File Provider I/O timeouts. Next: upload to website for download.
Resume file: None
Key milestone: v1.0.0-beta notarized and ready for distribution.
Next: Upload to Gaimer website, Windows build, ghost mode dragging, brain improvements, Phase 07 (Persistence), Phase 12 (Audio Intelligence Pipeline)
