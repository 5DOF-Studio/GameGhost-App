# Gaimer

**AI-powered gaming companion that watches your gameplay and provides real-time voice and text insights.**

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![MAUI](https://img.shields.io/badge/MAUI-MacCatalyst%20%7C%20Windows-blue)
[![.NET Tests](https://github.com/5DOF-Studio/GameGhost-App/actions/workflows/dotnet-test.yml/badge.svg?branch=main)](https://github.com/5DOF-Studio/GameGhost-App/actions/workflows/dotnet-test.yml)
![Tests](https://img.shields.io/badge/tests-519%20passing-brightgreen)
![Version](https://img.shields.io/badge/version-1.0.0--beta-orange)
![Phase](https://img.shields.io/badge/phase-11%2F12-yellow)
![Brain](https://img.shields.io/badge/brain-Claude%20Sonnet%204-blueviolet)
![License](https://img.shields.io/badge/license-Proprietary-red)

---

## Overview

Gaimer is a .NET 8 MAUI application that acts as an AI co-pilot for gamers. It captures your screen in real-time, analyzes gameplay through a multi-layered brain pipeline, and delivers contextual insights via voice and text — all through a native floating overlay that works on top of fullscreen games.

### AI Agents

| Agent | Game Type | Personality | Status |
|-------|-----------|-------------|--------|
| **Leroy** | Chess | Cocky knight-obsessed wildcard | Available |
| **Wasp** | Chess Master | Composed queen archetype, positional | Available |
| **Derek** | RPG / Adventure | — | Gated |

---

## Architecture

```
Screen Capture ──> Brain (sole image consumer)
                       │
                       ▼
                 Channel<BrainResult>
                       │
                       ▼
                 BrainEventRouter
                  ┌────┼────┐
                  ▼    ▼    ▼
              Timeline Voice Ghost
              (events)(text)(overlay)
```

### Key Design Principles

- **Brain is the sole consumer of visual data** — voice never receives raw images
- **All brain output flows through Channel&lt;BrainResult&gt;** — no direct writes to timeline or voice
- **Three-layer context model** — L1 (0-30s immediate), L2 (30s-5min rolling), L3 (5min+ narrative)
- **Session state machine** — OutGame/InGame with tool gating (3 tools out-game, 6 in-game)

### Tech Stack

| Layer | Technology |
|-------|-----------|
| **Framework** | .NET 8 MAUI (MacCatalyst + Windows) |
| **UI** | XAML + MVVM, dark gamer aesthetic |
| **Brain** | OpenRouter API (Claude Sonnet 4 vision + multi-turn tool calling) |
| **Voice** | Gemini Live API / OpenAI Realtime API |
| **Screen Capture** | ScreenCaptureKit (macOS native xcframework) |
| **Ghost Mode** | AppKit NSPanel (macOS native xcframework, vtool-retagged to Catalyst) |
| **Chess Engine** | Local Stockfish 17 (UCI protocol) |
| **Audio** | AVAudioEngine (macOS) / NAudio (Windows) |
| **Tests** | xUnit 2.9 + Moq 4.20 + FluentAssertions 6.12 (519 tests) |

---

## Features

### Live-Verified (Full Pipeline)

The complete brain pipeline has been verified live against Apple Chess.app — screen capture through Claude Sonnet 4 (OpenRouter) analysis through timeline display through Gemini Live voice narration, running stable for 10+ minutes.

### Completed (Phases 01-06, 08-11)

- **Agent Selection** — card-based UI with portraits, color themes, feature grids
- **Main Dashboard** — top bar (preview + live messages), sidebar connectors, bottom bar (audio + ghost)
- **Login/Invite + Agent Onboarding** — 4-state flow with download UX and flip animation
- **Chat Brain Architecture** — session state machine, timeline hierarchy, prompt builder, event router
- **Screen Capture** — ScreenCaptureKit native integration with perceptual diff gating (dHash)
- **Ghost Mode** — NSPanel floating overlay with FAB (agent portrait), event cards, click-through transparency
- **Brain Infrastructure** — OpenRouter client (Claude Sonnet 4 vision), tool executor (Lichess eval, web search), Channel pipeline
- **Capture Pipeline** — brain-only image consumption, L1/L2 context layers, diff gate dedup, per-agent config
- **Voice Integration** — Gemini Live + OpenAI Realtime providers with audio resampling
- **Stockfish Chess Engine** — local Stockfish 17 UCI engine, dual chess tools (engine + strategic), download UX
- **Agent Personality System** — 5-block architecture (Soul/Style/Behavior/Situations/Anti-Patterns), Leroy + Wasp composed
- **Settings Page** — bento grid layout (Voice Config, Active Voice, System Info, About)
- **Error Handling** — SystemError events routed to timeline + ghost overlay, global error page
- **UI Polish** — audio panel (4 toggles with feature guards), ghost FAB portrait, relative timestamps, circular button design, power button 3-state
- **519 Unit & Integration Tests** — full coverage across all implemented subsystems
- **CI/CD** — GitHub Actions dotnet-test workflow on push/PR

### Remaining

- **Phase 07** — Persistence (session history, settings storage)
- **Phase 12** — Audio Intelligence Pipeline (voice command, game audio, virtual mic)

---

## Project Structure

```
src/GaimerDesktop/
├── GaimerDesktop/                  # Main MAUI application
│   ├── Views/                       # XAML pages
│   ├── ViewModels/                  # MVVM view models
│   ├── Models/                      # Data models + Timeline hierarchy
│   ├── Services/                    # Core services (SessionManager, TimelineFeed, etc.)
│   ├── Services/Brain/              # Brain pipeline (OpenRouter, ToolExecutor, Channel)
│   ├── Services/Chess/              # Stockfish engine (UCI, download, FEN validation)
│   ├── Services/Conversation/       # Voice providers (Gemini, OpenAI, Mock)
│   ├── Controls/                    # Custom MAUI controls
│   ├── Resources/                   # Images, Fonts, Styles
│   └── NativeHelpers/               # Native xcframeworks (ScreenCapture, GhostMode)
│
├── GaimerDesktop.Tests/            # Test project (519 tests)
│   ├── Brain/                       # Brain pipeline tests
│   ├── Chess/                       # Stockfish, FEN, UCI, chess tools tests
│   ├── Session/                     # Session & state tests
│   ├── Timeline/                    # Timeline feed & icon map tests
│   ├── Prompts/                     # Prompt builder tests
│   ├── Audio/                       # Audio resampling & format tests
│   ├── Models/                      # Serialization & model tests
│   ├── Personality/                 # Agent personality composition tests
│   ├── ViewModels/                  # MainViewModel & AgentSelectionVM tests
│   └── Integration/                 # End-to-end & live API tests
│
GAIMER/GameGhost/                             # Documentation & handoff
├── AGENT_HANDOFF_INSTRUCTIONS.md    # Session continuity
├── PROGRESS_LOG.md                  # Development timeline
├── FEATURE_LIST.md                  # Feature checklist
├── TEST_COVERAGE_REPORT.md          # Test coverage report
└── gaimer_spec_docs/                # Architecture specs
```

---

## Build & Run

### Prerequisites

- .NET 8.0 SDK
- macOS: Xcode 15+ (for MacCatalyst)
- Windows: Windows 10 SDK 19041+

### Build

```bash
# macOS (MacCatalyst)
dotnet build src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj \
  -f net8.0-maccatalyst -p:EnableCodeSigning=false

# Windows
dotnet build src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj \
  -f net8.0-windows10.0.19041.0
```

### Run Tests

```bash
dotnet test src/GaimerDesktop/GaimerDesktop.Tests/GaimerDesktop.Tests.csproj -f net8.0
```

All 519 tests run via `net8.0` target (CoreCLR). Platform builds use Mono and are not compatible with the CLI test runner.

### Environment Variables

| Variable | Purpose |
|----------|---------|
| `OPENROUTER_APIKEY` | OpenRouter API key for brain pipeline |
| `GEMINI_APIKEY` | Google Gemini API key for voice |
| `OPENAI_APIKEY` | OpenAI API key for voice (alternative) |
| `VOICE_PROVIDER` | Force voice provider: `gemini`, `openai`, or `mock` |
| `USE_MOCK_SERVICES` | Set `true` to force all mock services |

---

## Test Coverage

| Category | Tests | Status |
|----------|-------|--------|
| Brain Pipeline | 42 | Passing |
| Chess (Stockfish, FEN, UCI, Tools) | 127 | Passing |
| ViewModels (Main + AgentSelection) | 72 | Passing |
| Voice & Auth Services | 33 | Passing |
| Session & State | 11 | Passing |
| Timeline | 11 | Passing |
| Prompt Building | 7 | Passing |
| Personality System | 30 | Passing |
| Audio | 11 | Passing |
| Models & Serialization | 10 | Passing |
| Integration & E2E | 6 | Passing |
| UI Polish & Regression | 21 | Passing |
| Live Testing & Error Handling | 17 | Passing |
| Settings & Configuration | 21 | Passing |
| **Total** | **519** | **All Passing** |

---

## Development

This project uses [GSD](https://github.com/cyanheads/gsd-framework) for phased execution planning. All phase plans are in `.planning/phases/`.

| Phase | Name | Plans | Status |
|-------|------|-------|--------|
| 01 | MainView V2 UI | 1/1 | Complete |
| 02 | Chat Brain Architecture | 6/6 | Complete |
| 03 | Screen Capture (ScreenCaptureKit) | 3/3 | Complete |
| 04 | Ghost Mode (Native Overlay) | 3/3 | Complete |
| 05 | Brain Infrastructure (OpenRouter) | 4/4 | Complete |
| 06 | Capture Pipeline | 3/3 | Complete |
| 07 | Persistence | — | Deferred |
| 08 | Polish | 5/5 | Complete |
| 09 | Stockfish Chess Engine | 4/4 | Complete |
| 10 | Integration Test Coverage | 4/4 | Complete |
| 11 | Agent Personality System | 6/6 | Complete |
| -- | Live Testing Sprint | -- | Complete |
| 12 | Audio Intelligence Pipeline | — | Research |

---

## License

Proprietary. Copyright 2025-2026 5DOF AI Studio.
