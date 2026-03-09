# Windows Overlay Implementation Plan (Future Work)

**Project:** Gaimer Desktop (.NET MAUI)  
**Audience:** Windows PC gamers  
**Status:** Planned (post–core functionality)  
**Last Updated:** December 13, 2025  

---

## Goals (The “Cluely-like” Wish)

Deliver a Windows-only overlay experience that:

- **Anchors** to the selected target app/game window (follows position/size).
- Is **always-on-top** above the target window.
- Is **click-through by default** so it never steals game input.
- Becomes **visible** (opacity up) during “actions”:
  - AI speaking (TTS)
  - New message / insight display
  - Explicit user invoke (hotkey)
- Optionally becomes **interactive** only when explicitly invoked (hotkey), otherwise remains click-through.

**Non-goals for v1:**
- macOS overlay parity (MacCatalyst limitations).
- Perfect support for **exclusive fullscreen** in all games (start with windowed/borderless fullscreen).

---

## Recommended Product Constraints (v1)

- **Windows only**
- Games supported in:
  - **Windowed**, or
  - **Borderless fullscreen** (a.k.a. “fullscreen windowed”)
- Overlay interaction is **hotkey-driven**:
  - Click-through while idle
  - Temporarily interactive when user invokes it

---

## Architecture Overview

### High-level Components

1. **Target Window Tracker (Windows native)**
   - Owns `HWND` of selected target window
   - Tracks bounds (`GetWindowRect`) at a polling interval and/or WinEvent hooks
   - Handles state changes (minimized, closed, moved monitors)

2. **Overlay Window Host (Windows native)**
   - A topmost, borderless window that can toggle:
     - **Click-through** (WS_EX_TRANSPARENT)
     - **Opacity** (layered window alpha)
     - **Visibility** (Show/Hide)
   - Moves/resizes overlay to match target via `SetWindowPos`

3. **Overlay UI Renderer (MAUI content)**
   - The “MinimalView-like” UI rendered inside the overlay host
   - Driven by the app’s state machine:
     - idle / action / invoked

4. **Overlay Controller (Cross-platform service interface, Windows impl first)**
   - Single API for app logic to request:
     - `ShowAction(content)`
     - `HideToStealth()`
     - `SetClickThrough(bool)`
     - `AttachToTarget(targetId)`
     - `Detach()`

---

## APIs & Techniques (Windows)

### Window tracking
- `EnumWindows`, `GetWindowText`, `GetClassName` (enumeration)
- `GetWindowRect` (position/size)
- Optional (v2): WinEvent hooks (`SetWinEventHook`) for move/size events instead of polling

### Overlay window behavior
- **Topmost**: `SetWindowPos(HWND_TOPMOST, ...)`
- **Click-through**: extended styles:
  - `WS_EX_LAYERED`
  - `WS_EX_TRANSPARENT`
  - (optional) `WS_EX_TOOLWINDOW` to keep out of Alt-Tab
- **Opacity**: `SetLayeredWindowAttributes` (alpha)

### Global hotkey
- `RegisterHotKey` + message loop handling
- Suggested defaults:
  - Toggle overlay: `Ctrl+Shift+Space`
  - “Pin visible for 10s”: `Ctrl+Shift+P`

---

## State Machine (Overlay UX)

### Inputs
- `IsConnected`
- `HasMessage` / `MessageArrived`
- `IsSpeaking`
- `HotkeyInvoked`
- `TargetWindowAlive`
- `TargetWindowIsForeground` (optional)

### States
- **Detached**: not connected or no target selected → overlay off
- **Stealth**: overlay attached + click-through + opacity very low/0
- **Action**: overlay visible (opacity high) but still click-through OR interactive (config)
- **Invoked**: overlay visible + interactive; times out back to stealth

### Default behaviors
- On message or speaking start:
  - enter **Action** for N seconds (e.g., 5–10s)
- On hotkey:
  - enter **Invoked** for M seconds (e.g., 10–20s)
- After timeout:
  - return to **Stealth**

---

## Phased Delivery Plan

### Phase 0 — Prereqs (Core app readiness)
- App already supports:
  - selecting a “target window/app”
  - connect/disconnect lifecycle
  - message events + (future) speaking events
- Confirm Windows build target (`net8.0-windows10.0.19041.0`) is stable.

### Phase 1 — Windows overlay spike (minimal viable overlay)
- Implement Windows-only overlay host window that:
  - attaches to one chosen `HWND`
  - tracks with `GetWindowRect` + `SetWindowPos` (polling every 100–250ms)
  - supports:
    - topmost
    - click-through toggling
    - opacity animation (simple step/linear)
- Use a placeholder UI (single label) to validate rendering pipeline.

**Exit criteria:**
- Overlay follows a normal app window reliably
- Click-through works (game/app receives clicks)
- Hotkey toggles visibility

### Phase 2 — Integrate “MinimalView UI” into overlay
- Render your MinimalView content in the overlay host
- Implement the overlay state machine:
  - stealth ↔ action ↔ invoked
- Add “expand/restore” behavior (via hotkey or minimal control surface)

**Exit criteria:**
- Overlay stays non-intrusive during gameplay
- Messages appear reliably and disappear back to stealth

### Phase 3 — Hardening (games + edge cases)
- Borderless fullscreen / multi-monitor
- DPI scaling correctness
- Alt-tab behavior
- Focus handling (overlay should not steal focus unless invoked)
- Target window closed/minimized handling

---

## Risks / Edge Cases

- **Exclusive fullscreen**: overlays may not appear or may be blocked depending on game/anti-cheat.
- **Anti-cheat**: some games may detect overlays; must be cautious and transparent with users.
- **DPI scaling**: coordinate transforms needed on high-DPI / mixed-DPI monitors.
- **Performance**: polling too frequently can be expensive; WinEvent hooks can reduce load.

---

## Acceptance Criteria (v1)

- [ ] User selects a target window and connects
- [ ] Overlay appears aligned to target window and follows it
- [ ] Overlay is click-through while idle (game input unaffected)
- [ ] On message/speaking, overlay becomes visible (opacity increases) for a configurable duration
- [ ] Hotkey toggles “interactive/invoked” mode without breaking the game

---

## Notes on macOS

macOS parity requires native AppKit overlay techniques (e.g., `NSPanel` with `ignoresMouseEvents`) and typically cannot be fully achieved with MacCatalyst. This is intentionally deferred until Windows value is proven.


