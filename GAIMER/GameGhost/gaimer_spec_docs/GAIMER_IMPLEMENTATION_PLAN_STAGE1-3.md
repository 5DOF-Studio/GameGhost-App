# Gaimer Desktop - Implementation Plan (Stages 1–3)

**Version:** 1.1.0  
**Date:** December 10, 2024  
**Scope:** Environment setup, initial repo baseline, UI build-out with mock data, and first successful test build for the Gaimer desktop app (built on the Witness platform).

---

## Implementation Status Summary

| Stage | Status | Completion |
|-------|--------|------------|
| Stage 0 – Environment Setup | ✅ Complete | 100% |
| Stage 1 – Repository Baseline | ✅ Complete | 100% |
| Stage 2 – UI Build-Out | ✅ Complete | 95% |
| Stage 3 – Mock Data & First Build | ✅ Complete | 90% |

### Known Defects
- **BUG-001:** Audio bars not animating (Medium) - IN/OUT volume indicators don't update
- **BUG-002:** Auto-disconnect on MinimalView return (High) - Expanding from MinimalView causes disconnect
- **BUG-003:** macOS Catalyst window sizing (Medium) - Fixed-size enforcement unreliable; current workaround is resizable main window (default 1200×900, min 900×720)

### Pending Items
- MinimalView state sharing with MainView
- Audio bar UI binding fix
- Real window enumeration (currently mock)

---

## 1. Objectives & Scope

- Establish a **repeatable development environment** for Witness/Gaimer on Windows and macOS.
- Create an **initial repository baseline** aligned with `GAIMER_DESKTOP_APP_SPEC.md` and `gaimer_design_spec.md`.
- Build out the **core Gaimer UI** (Agent Selection, Main Dashboard, Minimal Connected View) using **mock data and services**.
- Achieve a **successful local build and run** using mock functionality only (no real Gemini/audio/capture yet).
- Define a **documentation and logging routine** (including updates to `PROGRESS_LOG.md`).

These stages map primarily to:
- Witness Progress Log: **Phase 1 – Foundation** (UI + project setup with Gaimer-specific layouts)
- Gaimer Spec: **Phases 1–3** (Core UI, Agent System, partial Window Capture via mock data)

---

## 2. Stage 0 – Environment Setup

### 2.1 Goals

- Ensure developers can **build and run** a .NET MAUI desktop app (Witness/Gaimer) on:
  - Windows 10/11 (x64/ARM64)
  - macOS 12+ (Intel/Apple Silicon)
- Configure **Gemini API credentials** for later integration.
- Set up the **UI mockup** environment as a visual reference.

### 2.2 Tasks

- **Install core tooling**
  - Install **.NET 8 SDK** (per `GAIMER_DESKTOP_APP_SPEC.md`).
  - Install MAUI workloads (on each dev machine):
    - `dotnet workload install maui`
  - Install a supported IDE:
    - Windows: Visual Studio 2022 (with .NET MAUI workload), Rider, or VS Code + C# Dev Kit.
    - macOS: Visual Studio for Mac (if available), Rider, or VS Code + C# Dev Kit.

- **Git and repository**
  - Ensure Git is installed and configured (name/email).
  - Clone the `gAImer_desktop` repository locally.
  - Verify `.gitignore` covers:
    - `bin/`, `obj/`, `.vs/`, `.idea/`, `.vscode/`, user-secrets, any local config files.

- **Gemini API configuration (for later stages)**
  - Decide on configuration mechanism:
    - `.NET user-secrets` for local development **or**
    - Environment variable (e.g., `GEMINI_API_KEY`).
  - Ensure the **planned `IConfiguration` usage** in `GeminiLiveService` will read from:
    - `appsettings.Development.json` (optional),
    - `user-secrets` (for local),
    - or environment variables.
  - Document the chosen approach in a short dev note (to be referenced in a later documentation pass).

- **UI mockup setup**
  - From `GAIMER/GameGhost/gaimer_spec_docs/ui_mockup`:
    - Option 1: Open `index.html` directly in a browser.
    - Option 2 (preferred): Run a simple static server:
      - `python -m http.server 8080` (or equivalent).
  - Use this mockup as the **visual contract** for MAUI:
    - Colors, layout, states (offline/connecting/connected),
    - Agent selection, game selector, minimal view behavior.

### 2.3 Definition of Done

- Developers on both Windows and macOS can:
  - Build a **hello-world MAUI app**.
  - Open and interact with the **UI mockup**.
- Repository cloned locally with Git configured.
- Decision recorded on **where Gemini API keys live** for dev.

---

## 3. Stage 1 – Repository Baseline & Initial Commit

### 3.1 Goals

- Create a **clean .NET MAUI solution** aligned with `GAIMER_DESKTOP_APP_SPEC.md`.
- Include the **Gaimer-specific specifications** (`gaimer_design_spec.md`, UI mockup).
- Make an **initial commit** that becomes the anchor point for further work.

### 3.2 Tasks

- **Solution and project structure**
  - Create MAUI solution: `GaimerDesktop.sln`.
  - Add primary MAUI project: `src/GaimerDesktop/`.
  - Add optional shared project: `src/GaimerDesktop.Core/` for constants/configuration.
  - Ensure folder layout matches the canonical spec:
    - `Views/`, `ViewModels/`, `Services/`, `Models/`, `Platforms/Windows`, `Platforms/MacCatalyst`, `Controls/`, `Utilities/`, `Resources/`.

- **Baseline application wiring**
  - Implement `App.xaml` and `AppShell.xaml` with:
    - Shell registration for:
      - `AgentSelectionPage`
      - `MainPage` (Gaimer Dashboard)
      - `MinimalViewPage` (compact connected UI)
  - Add `MauiProgram.cs` with:
    - Font registrations (`Orbitron`, `Rajdhani`).
    - DI for core services/interfaces (placeholders acceptable at this stage).

- **Dependencies**
  - Add NuGet references as per platform spec:
    - `CommunityToolkit.Mvvm`
    - `SkiaSharp.Views.Maui.Controls`
    - Conditional Windows-only: `NAudio` (even if not yet used).

- **Specs and documentation inclusion**
  - Ensure the following are included and tracked under `GAIMER/GameGhost/`:
    - `GAIMER_DESKTOP_APP_SPEC.md`
    - `gaimer_spec_docs/gaimer_design_spec.md`
    - `gaimer_spec_docs/ui_mockup/*`
    - `GAIMER_IMPLEMENTATION_PLAN_STAGE1-3.md` (this document).

- **Initial commit**
  - Verify the solution builds (empty or placeholder pages).
  - Commit with message along the lines of:
    - `chore: initialize Witness/Gaimer MAUI solution and specs`

### 3.3 Definition of Done

- `GaimerDesktop.sln` and core project(s) exist and build successfully.
- GAIMER/Witness specs and this implementation plan are tracked in Git.
- One clear **initial commit** forms the base of subsequent feature branches.

---

## 4. Stage 2 – UI Build-Out (Gaimer Views, Mocked)

### 4.1 Goals

- Implement the **core Gaimer UI** (no real platform services yet):
  - Agent Selection Screen.
  - Main Dashboard (offline and basic connected states).
  - Minimal Connected View.
- Use **mock data** in ViewModels to simulate:
  - Agents (General, Chess).
  - Window capture targets (fake thumbnails and titles).
  - Connection states and audio levels.

### 4.2 Phase 2.1 – Shared UI Foundation

- **Design tokens**
  - Port key CSS variables from `styles.css` into XAML:
    - Colors → `Colors.xaml`.
    - Typography and basic styles → `Styles.xaml`.
  - Ensure names align with `GAIMER_DESKTOP_APP_SPEC.md` (e.g., `BgPrimary`, `AccentCyan`, etc.).

- **Base layout framing**
  - Implement **shared container frames**:
    - App background.
    - Card/border styles.
    - Buttons (primary/secondary, connect states).

**Acceptance criteria**
- Application uses centralized **resource dictionaries** for colors, fonts, and common styles.
- Typography and basic spacings match the mockup reasonably closely.

### 4.3 Phase 2.2 – Agent Selection Screen

- **View**
  - Create `AgentSelectionPage` mirroring Section 6.1 of `gaimer_design_spec.md`:
    - Header with logo, tagline (“gaimer – Built on Witness platform”).
    - Two agent cards: **General Gaimer** and **Chess Gaimer**.
    - Card hover/selected visual states approximated in MAUI.

- **ViewModel**
  - Create `AgentSelectionViewModel` with:
    - `ObservableCollection<AgentViewModel>` (backed by the Agent definition in the Gaimer spec).
    - Commands:
      - `SelectAgentCommand(AgentViewModel)` → navigates to `MainPage` with selected agent.

- **Mock data**
  - Hard-code the two initial agents (General and Chess) with properties from Section 2 of `gaimer_design_spec.md`.

**Acceptance criteria**
- App launches into **Agent Selection**.
- Selecting an agent navigates to **Main Dashboard** with agent context stored in ViewModel state.

### 4.4 Phase 2.3 – Main Dashboard (Offline & Mock Connected)

- **View**
  - Implement `MainPage` layout per Sections 3.3, 6.2–6.6 of `gaimer_design_spec.md`:
    - Header: logo, agent badge, connection status, settings icon.
    - Content row: left preview container, right game selector panel.
    - Audio section: input/output percentages and visualizer placeholder.
    - Footer: agent name, version, Gemini Live API label, Connect button.

- **ViewModel**
  - Create/extend `MainViewModel` to include:
    - `SelectedAgent`.
    - `CaptureTargets` (list of mock window targets).
    - `SelectedTarget`.
    - `ConnectionState` (enum per Gaimer spec).
    - `InputVolume` / `OutputVolume` (floats 0–1).
    - Derived/UI properties:
      - Connection badge text/color.
      - Connect button text/color.
      - Flags for “No Game Selected” vs. “Active Preview” states.

- **Mock data and behavior**
  - Populate `CaptureTargets` with static entries:
    - Example: Chess.com (browser), Discord, Steam, etc., each with a placeholder thumbnail.
  - Implement `ToggleConnectionCommand` using an **in-memory mock**:
    - Offline → Connecting → Connected (timed transitions).
    - Simulate volume changes using a timer to update `InputVolume`/`OutputVolume`.
    - Display sliding info panel content using mock messages (“AI Insight”).

**Acceptance criteria**
- From Agent Selection:
  - Agent badge + colors update based on selected agent.
- Selecting a mock game shows:
  - Game selector state as selected.
  - Preview state transitions from empty → “Live” HUD with placeholder image.
- Clicking **Connect** cycles through offline/connecting/connected states with visible UI updates and fake audio levels.

### 4.5 Phase 2.4 – Minimal Connected View

- **View**
  - Implement `MinimalViewPage` mirroring Section 6.7 of `gaimer_design_spec.md`:
    - Agent profile with icon + glow.
    - Current game info and audio levels.
    - Compact visualizer.
    - Live indicator + Disconnect button.
    - Sliding panel for AI insight.

- **Navigation**
  - From `MainPage`, provide an action to **switch to Minimal View** when connected (e.g., button or menu action).
  - From `MinimalViewPage`, allow:
    - Expand back to full Dashboard.
    - Disconnect and return to Dashboard offline.

- **Mock behavior**
  - Reuse the same mock connection/volume state as `MainPage`.
  - Maintain consistent state across pages (shared ViewModel or shared state service).

**Acceptance criteria**
- When “connected” in Dashboard:
  - User can open Minimal View with consistent agent/game/connection info.
- Disconnecting from Minimal View correctly resets state when returning to Dashboard.

---

## 5. Stage 3 – Mock Data & First Successful Build

### 5.1 Goals

- Replace ad-hoc mock logic with **explicit mock service implementations** for:
  - `IAudioService`
  - `IWindowCaptureService`
  - `IGeminiService`
- Achieve a **repeatable build and run workflow** using these mocks for early QA and UI iteration.

### 5.2 Tasks

- **Mock service implementations**
  - Add `MockAudioService`:
    - `StartRecordingAsync` → periodically raises fake audio buffers and volume events.
    - `PlayAudioAsync` → no-op or simple delay with volume updates.
  - Add `MockWindowCaptureService`:
    - `GetCaptureTargetsAsync` → returns a static list of `CaptureTarget` objects.
    - `StartCaptureAsync` → periodically invokes `onFrameCaptured` with a static or small set of sample JPEGs.
  - Add `MockGeminiService`:
    - `ConnectAsync` / `DisconnectAsync` → state transitions only.
    - `SendAudioAsync` / `SendImageAsync` → log calls, optionally trigger fake “AI audio” via `AudioReceived`.

- **Development-time DI configuration**
  - In `MauiProgram.cs`, introduce a **development profile**:
    - Default: register mock services.
    - Later: production profile registers real platform-specific services.
  - Optionally, use compiler directives or config flag to toggle.

- **Build and run smoke tests**
  - Verify build on:
    - macOS (`dotnet build`, run Mac Catalyst target).
    - Windows (when environment is available).
  - Manual test flows:
    - Agent selection → Dashboard (offline).
    - Select game → Connect → Minimal View.
    - Observe mocked volume/visualizer and sliding info panel.

### 5.3 Definition of Done

- All three Gaimer views (Agent Selection, Dashboard, Minimal View) are **navigable and interactive** with mock data.
- Build succeeds on at least one platform (macOS initially, Windows as soon as available).
- No dependency yet on real Gemini or native audio/capture – safe for UI-focused iteration.

---

## 6. Documentation, Logging, and Progress Updates

### 6.1 Documents to Maintain

- `GAIMER/GameGhost/GAIMER_DESKTOP_APP_SPEC.md`  
  - Canonical platform-level spec. Only updated if architecture or cross-app behavior changes.

- `GAIMER/GameGhost/gaimer_spec_docs/gaimer_design_spec.md`  
  - Gaimer-specific UX and product specification.

- `GAIMER/GameGhost/gaimer_spec_docs/GAIMER_IMPLEMENTATION_PLAN_STAGE1-3.md`  
  - This implementation plan for the first three stages (environment, initial baseline, UI with mocks).

- `GAIMER/GameGhost/PROGRESS_LOG.md`  
  - Project-wide phase/milestone tracking and weekly updates.

### 6.2 Update Routine per Significant Milestone

For each completed sub-stage or notable step (e.g., “Stage 1 complete”, “Dashboard UI built with mocks”):

- **Git**
  - Commit changes with clear messages (examples):
    - `feat: add gaimer agent selection page and mock data`
    - `feat: implement main dashboard layout with mock targets`
    - `chore: add mock services for audio, capture, and gemini`

- **Progress log (`PROGRESS_LOG.md`)**
  - Update relevant **Phase 1** entries from “Not Started” to “In Progress” / “Completed” as appropriate.
  - Add concise notes under **Progress Notes** indicating:
    - Environment setup status.
    - UI build-out milestones achieved.
    - Mock build and smoke test results.
  - Append to the **Change Log** section with date-stamped entries summarizing:
    - Plan creation.
    - Major Gaimer UI milestones.

- **Ad-hoc notes (if needed)**
  - If any deviations from spec occur, note them briefly in:
    - Either a new `NOTES.md` under `GAIMER/GameGhost/gaimer_spec_docs/` (if needed later), or
    - Within `gaimer_design_spec.md` under a “Notes / Deviations” section.

### 6.3 Initial Log Entry (upon adopting this plan)

When this plan is adopted, add to `PROGRESS_LOG.md`:

- Mark **Project Setup** under Phase 1 as “In Progress” once environment setup begins.
- Add a Change Log entry similar to:
  - `- Added GAIMER implementation plan (Stages 1–3) and aligned Witness/Gaimer early-phase roadmap.`

---

## 7. Next Steps After Stage 3

Beyond this document, subsequent stages will cover:

- Replacing mock services with **real platform implementations** (audio/capture).
- Integrating **live Gemini WebSocket**.
- Hardening error handling, reconnection, and performance.
- Preparing for **packaging and distribution** workflows as described in `GAIMER_DESKTOP_APP_SPEC.md`.


