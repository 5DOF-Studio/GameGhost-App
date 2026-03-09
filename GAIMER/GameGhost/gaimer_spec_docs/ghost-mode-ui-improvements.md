# Ghost Mode UI Improvements Checklist

**Created:** 2026-02-27
**Status:** Planned
**Scope:** Native overlay (FabView.swift, EventCardView.swift, GhostPanelContentView.swift) + MAUI FAB (FabOverlayView.xaml, MainPage.xaml)

---

## 1. Profile Picture — Use Correct Agent Portrait

**Current:** `PortraitImage` uses `leroy_chess_master_icon.png` (icon-style), `derek_adventurer_icon.png`, `default_agent.png`
**Problem:** These may not be the intended profile pictures for the ghost mode FAB. Derek has a separate `derek_profile_pic.png` that goes unused as portrait.

### Tasks
- [ ] **1a.** Audit available images: `leroy_chess_master_icon.png`, `derek_adventurer_icon.png`, `derek_profile_pic.png`, `default_agent.png`
- [ ] **1b.** Decide correct portrait per agent (icon vs profile pic vs new asset)
- [ ] **1c.** Update `Agent.cs` PortraitImage values if needed (lines 38, 66, 95)
- [ ] **1d.** Verify native FAB renders the correct image (NSImage load from bundle path)

**Files:** `Models/Agent.cs`, `Resources/Images/`

---

## 2. Increase Ghost Mode Button Size in MainPage

**Current:** FAB is 64x64 outer / 56x56 inner circle, sitting in the right column of the bottom bar (Grid.Column="1") which is `*` width. The audio panel (left column) is fixed at 340px.
**Goal:** Reduce the audio panel width to give the circular ghost button more room — equivalent to user profile picture size (100x100).

### Tasks
- [ ] **2a.** Increase FAB size: 64x64 → ~108x108 outer, 56x56 → ~100x100 inner (`FabOverlayView.xaml`)
  - Update WidthRequest/HeightRequest on outer Grid (line 9)
  - Update outer Border glow ring to match (lines 12-13, CornerRadius line 19)
  - Update inner Border circle (lines 30-31, CornerRadius line 37)
  - Update Image clip geometry center/radii (line 50)
  - Update X icon font size proportionally (line 55)
- [ ] **2b.** Update FabOverlayView size in MainPage.xaml (lines 768-769): `WidthRequest="108" HeightRequest="108"`
- [ ] **2c.** Reduce audio panel left column width: `ColumnDefinitions="300,*"` or similar (MainPage.xaml line 677)
- [ ] **2d.** Increase native FAB size to match: FabView.swift `fabSize` from 56 to match (line 35, and all derived geometry)
- [ ] **2e.** Update GhostPanelContentView.swift FAB layout calculations (lines 100-103)

**Files:** `Views/FabOverlayView.xaml`, `MainPage.xaml` (lines 677, 764-769), `FabView.swift`, `GhostPanelContentView.swift`

---

## 3. Update Ghost Button States

**Current states:**
- Disconnected: 0.3 opacity, no glow
- Connected (IsFabEnabled): yellow glow ring, 1.0 opacity, shows agent portrait
- Active (IsFabActive): shows X icon instead of portrait

**New state design:**

| State | Visual | Label |
|-------|--------|-------|
| **Off-Game** (not connected) | Yellow button background | "Ghost Mode" (black text) |
| **In-Game** (connected, not active) | Agent profile picture, highlighted circle border | None |
| **Ghost Active** (FAB tapped) | Agent profile image (NOT X icon) | None |

### Tasks
- [ ] **3a.** Off-Game state: Yellow background circle + "Ghost Mode" black text label
  - MAUI: Update `FabOverlayView.xaml` — add Label "Ghost Mode", yellow BG when disconnected
  - Native: Update `FabView.swift` — yellow fill + text rendering when not connected
- [ ] **3b.** In-Game state: Agent portrait with highlighted circle border (accent glow)
  - Already close to current connected state, verify border is prominent enough
- [ ] **3c.** Ghost Active state: Keep agent profile image visible (remove X icon)
  - MAUI: Remove/hide X icon Label (line 54-60), always show portrait when active
  - Native: FabView.swift — remove X icon drawing (lines 117-134), keep portrait visible in active state
  - May want visual distinction (e.g., slight dim, different glow color, or pulsing) to indicate active

**Files:** `Views/FabOverlayView.xaml`, `FabView.swift`

---

## 4. Ghost Button Pop-out Card Redesign

**Current card design:**
- Title pill: Cyan "AI INSIGHT" label at top
- Text body below
- Voice card: avatar + "is talking" + animated dots

**New card design per message type:**

### 4A. Text/Image Message Card
- [ ] **4Aa.** Remove "AI INSIGHT" label — replace with **message type icon** (from EventIconMap)
  - Icon placement: top-leading corner
  - Icons available: alert.png, opportunity.png, sage.png, assessment.png, chat.png, etc.
  - Need to pass EventOutputType through to native layer (new parameter on ShowCard)
- [ ] **4Ab.** Layout hierarchy (vertical):
  1. **Top-leading:** Message type icon (small, e.g. 20x20)
  2. **Middle:** Message text body
  3. **Bottom (if voice active):** Horizontal audio bars (same style as MainPage audio bars)

### 4B. Voice + Message Card (voice active AND text message present)
- [ ] **4Ba.** Layout hierarchy (vertical):
  1. **Top-leading:** Message type icon
  2. **Middle:** Message text
  3. **Bottom:** Horizontal audio bars (waveform visualization)
- [ ] **4Bb.** Audio bars style: horizontal alignment, same visual as MainPage bars (12 bars at varying heights)

### 4C. Voice Only Card (no text message, just voice activity)
- [ ] **4Ca.** Layout hierarchy (vertical):
  1. **Top-leading:** Voice/message type icon (if supported — most voice responses will map to a message type)
  2. **Middle:** Horizontal audio bars (larger, more prominent since no text)
- [ ] **4Cb.** Consider: voice-only may still get categorized as a message type from the AI response

### 4D. Card Opacity (Nice-to-Have)
- [ ] **4Da.** Increase card background opacity for better readability over game
  - Current: `#1E1E2E @ 0.95` (EventCardView.swift line 21)
  - Try slightly more opaque or add a subtle background blur
  - Test with actual gameplay behind to find the right balance

**Files:** `EventCardView.swift`, `GhostPanelContentView.swift`, `GhostPanelState` (needs new fields for icon + voice state), `GaimerGhostMode.swift` (@_cdecl API changes), `GhostModeNativeMethods.cs`, `MacGhostModeService.cs`, `IGhostModeService.cs`, `MainViewModel.cs`

---

## 5. Default Position — Top-Right (HUD Style)

**Current:** FAB defaults to bottom-right, card appears above FAB
**Goal:** Default to topmost-rightmost point. HUD = "heads up" = top of screen. Unobtrusive.

### Tasks
- [ ] **5a.** Change default FAB position in GhostPanelContentView.swift
  - Current: `x = bounds.width - 56 - 16`, `y = 16` (bottom-right in flipped coords)
  - New: Top-right of screen (AppKit coords: y = near top of panel frame)
- [ ] **5b.** Update GhostPanel.swift panelFrame if needed — ensure panel covers top-right area
- [ ] **5c.** Verify position works with fullscreen games and multiple monitors

**Files:** `GhostPanelContentView.swift` (layout method), `GhostPanel.swift`, `GaimerGhostMode.swift` (panel frame)

---

## 6. Card Position — Below FAB (Not Above)

**Current:** Card appears above the FAB (y = fabY + fabSize + gap)
**Reason for change:** With FAB at top-right, card above would be off-screen or clipped. Card must appear below FAB.

### Tasks
- [ ] **6a.** Update card Y position in GhostPanelContentView.swift
  - Current: `y = 16 + 56 + 12` (above FAB in bottom-right layout)
  - New: Card Y = fabY - cardHeight - gap (below FAB in top-right layout, AppKit coords)
  - Must account for dynamic card height (intrinsicContentSize)
- [ ] **6b.** Update slide-in animation direction
  - Current: slides in from right
  - New: may want slide-down from FAB or keep slide-from-right
- [ ] **6c.** Ensure card stays on-screen for tall cards (TextWithImage variant)

**Files:** `GhostPanelContentView.swift` (layout + showCard methods)

---

## 7. Draggable FAB — Move Around Screen

**Current:** Fixed position, not draggable
**Goal:** User can drag the FAB to reposition anywhere on screen. Card follows.

### Tasks
- [ ] **7a.** Add drag gesture to FabButtonView (FabView.swift)
  - Override `mouseDragged(_ event:)` to track delta movement
  - Update FAB frame position on drag
  - Distinguish drag from tap (minimum movement threshold, e.g. 5pt)
- [ ] **7b.** Notify GhostPanelContentView of new FAB position so card repositions accordingly
  - Card should appear below FAB at new position
  - If FAB is near bottom edge, card may need to flip above
- [ ] **7c.** Persist last FAB position (optional, nice-to-have)
  - UserDefaults key for saved position
  - Restore on next ghost_panel_create
- [ ] **7d.** Edge snapping (optional, nice-to-have)
  - Snap FAB to screen edges when released within threshold
  - Prevents FAB from being left in awkward mid-screen positions

**Files:** `FabView.swift` (drag handling), `GhostPanelContentView.swift` (layout recalculation), `GhostPanelState` (position tracking)

---

## Implementation Priority

| Priority | Item | Complexity | Notes |
|----------|------|------------|-------|
| P0 | 1. Profile picture | Low | Config change, verify |
| P0 | 3. Button states | Medium | MAUI + native changes |
| P0 | 5. Top-right position | Low | Layout coordinate change |
| P0 | 6. Card below FAB | Low | Layout coordinate change |
| P1 | 2. Increase button size | Medium | MAUI + native + layout |
| P1 | 7. Draggable FAB | Medium | Mouse event handling |
| P2 | 4A-C. Card redesign | High | New P/Invoke params, icon passing, audio bars in native |
| P3 | 4D. Card opacity | Low | Tweak one value + test |

---

## API Changes Required

Adding message type icon and voice state to cards requires extending the P/Invoke bridge:

```
// Current
ghost_panel_show_card(int variant, string? title, string? text, string? imagePath)

// Proposed
ghost_panel_show_card(int variant, string? title, string? text, string? imagePath, string? iconPath, bool voiceActive)
```

Or add separate functions:
```
ghost_panel_set_card_icon(string iconPath)
ghost_panel_set_voice_active(bool active)
```

The separate-function approach follows the existing pattern (set_fab_active, set_fab_connected) and avoids changing existing signatures.

---

## References

- Current native code: `NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/`
- MAUI FAB: `Views/FabOverlayView.xaml`
- MainPage bottom bar: `MainPage.xaml` lines 676-772
- Agent definitions: `Models/Agent.cs`
- Event icons: `Models/Timeline/EventIconMap.cs`
- Design spec: `GAIMER/GameGhost/gaimer_spec_docs/ghost-mode-design.md`
- Technical report: `GAIMER/GameGhost/gaimer_spec_docs/ghost-mode-technical-report.md`
