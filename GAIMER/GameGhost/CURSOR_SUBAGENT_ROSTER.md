# Cursor Subagent Roster (Claude-Equivalent)

This document defines the Cursor-side equivalent of your Claude specialist system without creating a duplicate office folder.

> Important: Cursor currently exposes built-in subagent types.  
> We emulate custom specialists by using consistent role prompts and ownership boundaries.

---

## Operating Model

- **PM Orchestrator role** drives planning, assignment, and acceptance.
- **Specialist roles** run as scoped execution passes using built-in Cursor subagents.
- **No duplicate folder structure** required; this file is the single source for role behavior in this repo.

---

## Specialist Roster

## 1) PM Orchestrator
- **Purpose:** Manage roadmap, assign work, enforce handoffs, track decisions.
- **Primary subagent mapping:** `gsd-planner` (planning), `gsd-plan-checker` (quality), `gsd-integration-checker` (cross-track).
- **Owns:**
  - backlog priority (`P0/P1/P2`)
  - sprint sequencing
  - acceptance criteria and go/no-go
- **Inputs:** product goals, constraints, current repo state.
- **Outputs:** plan docs, ticket board, implementation waves.

## 2) Frontend Engineer
- **Purpose:** Deliver UI/UX implementation and acquire missing technical patterns needed per task.
- **Primary subagent mapping:** `gsd-executor` for implementation, `explore` for discovery.
- **Owns:**
  - XAML layouts and styling consistency
  - component behavior and interaction states
  - skill acquisition pass for new UI tasks (examples, patterns, component architecture)
- **Inputs:** design brief, figma outputs, product acceptance criteria.
- **Outputs:** working UI code, notes on design-to-code deviations.
- **Skill Stack:**
  | Skill | Location | Role |
  |-------|----------|------|
  | `frontend-design-aesthetic` | `~/.cursor/skills/frontend-design-aesthetic/` | Creative direction — aesthetic choices, visual identity, anti-"AI slop" guidance |
  | `component-design-patterns` | `~/.claude/skills/component-design-patterns/` | Technical patterns — radial layouts, parallax, scroll animations, hover effects (React/Tailwind) |
  | `implement-design` (Figma) | Figma MCP plugin | Pixel-perfect Figma-to-code translation |
  | `create-design-system-rules` (Figma) | Figma MCP plugin | Project-specific design system conventions |

## 3) Chronicler
- **Purpose:** Maintain technical continuity and documentation quality.
- **Primary subagent mapping:** `generalPurpose` (doc updates) + `gsd-codebase-mapper` (state summaries when needed).
- **Owns:**
  - decision logs
  - progress logs
  - implementation notes and handoff docs
- **Inputs:** merged changes, PM decisions, milestone status.
- **Outputs:** updated docs in `GAIMER/GameGhost/` and `GAIMER/GameGhost/gaimer_spec_docs/`.

## 4) Showcaser (GitHub Readiness)
- **Purpose:** Ensure repo presentation and release hygiene before pushes/releases.
- **Primary subagent mapping:** `generalPurpose`.
- **Owns:**
  - README quality
  - release notes/changelog prep
  - PR summary/test plan quality checks
- **Inputs:** diff scope, release intent, target audience.
- **Outputs:** release-facing docs and review artifacts.

---

## Invocation Protocol (Cursor)

Use this language pattern in requests to trigger role behavior:

- **PM:** "PM mode: plan phase X and assign specialists."
- **Frontend Engineer:** "Frontend engineer pass: implement UI for X and acquire needed patterns."
- **Chronicler:** "Chronicler pass: update progress/feature/spec docs for X."
- **Showcaser:** "Showcaser pass: prepare repo/PR/release artifacts for X."

---

## Standard Handoff Contract

Every specialist output should include:

1. **What changed**
2. **Why it changed**
3. **What remains**
4. **Risks/assumptions**
5. **Validation steps**

---

## MCP Tooling (Global)

| MCP Server | Purpose | Tools |
|------------|---------|-------|
| **Superpowers** (`@superpowers/mcp-server`) | Expert-crafted workflow skills library | `find_skills`, `use_skill`, `get_skill_file` — TDD, systematic debugging, code review, brainstorming, git worktree |
| **Figma** (`plugin-figma-figma`) | Design-to-code pipeline | `get_design_context`, `get_screenshot`, `get_metadata` |
| **Browser** (`cursor-ide-browser`) | Frontend testing and web automation | `browser_navigate`, `browser_snapshot`, `browser_click`, etc. |
| **n8n** (StreamableHTTP) | Workflow automation | Project-specific n8n workflows |

> **Superpowers + GSD:** Superpowers provides *methodology* (how to approach a task); GSD subagents provide *execution* (doing the work). Load a Superpowers skill to guide a GSD executor's approach when needed.

---

## Current Gaimer Priority Mapping

### Recent Milestone (Feb 23, 2026)
- **Agent Selection Card Redesign:** Complete. Bold portrait cards with color-themed glows, chevron navigation, centered layout.
- **Asset Organization:** Created `assets/` folder as git-tracked design asset repository. Established naming conventions.

### Session Handoff (Feb 23, 2026)

| Item | Details |
|------|---------|
| **What changed** | AgentSelectionPage redesigned with portrait cards, chevron nav. Asset folder created. Agent model updated with PortraitImage/IconImage. |
| **Why** | Bolder, more gamey agent selection UX. Centralized asset management for git tracking. |
| **What remains** | Uncommitted changes in MainPage, MainViewModel, MinimalViewPage, MauiProgram, csproj, Controls/ folder (from prior sessions). |
| **Risks/assumptions** | Wasp agent still uses `default_agent.png` placeholder. Need Wasp portrait asset when available. |
| **Validation** | App builds and launches via codesign workaround. Agent cards display correctly with chevron navigation. |

### Active Priorities
- **PM:** `CHAT_V2_EXECUTION_TICKET_BOARD.md` execution and scope control.
- **Frontend Engineer:** ~~Agent Selection V2~~ ✅ → MainView/MinimalView V2 visual and interaction polish.
- **Chronicler:** keep `PROGRESS_LOG.md`, `FEATURE_LIST.md`, and Chat/Brain specs in sync.
- **Showcaser:** package changes into release-quality summaries when build is stable.

