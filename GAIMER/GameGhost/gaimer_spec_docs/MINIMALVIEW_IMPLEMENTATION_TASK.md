# MinimalView Implementation Task

**Project:** Gaimer Desktop (.NET MAUI)  
**Date:** December 10, 2024  
**Priority:** High  
**Assigned To:** Completed - December 12, 2024

---

## Context

Gaimer is a desktop AI gaming companion built on the Witness platform using .NET MAUI. The application has three main views:

1. **AgentSelectionPage** - Choose AI personality (General/Chess) ✅ Complete
2. **MainPage (Dashboard)** - Full dashboard with preview, game selector, AI insights ✅ Complete
3. **MinimalViewPage** - Compact connected view for active gaming ⚠️ Needs Reimplementation

The MinimalView currently exists but has architectural issues:
- Window doesn't resize (stays at 820×720 instead of 480×Auto)
- State management issues when returning to MainView (BUG-002)
- UI doesn't match the spec's compact design

---

## Task: Implement MinimalView with Hybrid Window Approach

### Approach Decision

We're adopting a **Hybrid Approach**:

1. **Window resizes** when transitioning to/from MinimalView
2. **MinimalViewPage has its own dedicated XAML** (not a shrunk MainPage)
3. **Sliding panel for AI messages/images** within the view
4. **Dynamic height** when panel is expanded (optional enhancement)

### Window Dimensions (Final Implementation - Dec 12, 2024)

| State | Width | Height | Notes |
|-------|-------|--------|-------|
| Main Dashboard | 1200px (default) | 900px (default) | Resizable; minimum 900×720 (updated from 820px for better layout) |
| Minimal View | 960px | 350px | Wide format for inline message display |

> ⚠️ **Known Issue:** macOS Catalyst uses device-independent units (points) and may restore prior window sizes.  
> Current approach: main window is resizable; default 1200×900; minimum 900×720.

---

## Required UI Elements (Match Spec Design)

### Reference: `gaimer_design_spec.md` Section 6.7

```
┌────────────────────────────────────────────────────────────┐
│ ┌──────┐                                                   │
│ │ 🎮   │  General Gaimer        [🎤 AUDIO]      [⤢]       │
│ │(icon)│  🖥️ Chess.com          IN: 45%  OUT: 72%         │
│ └──────┘                                                   │
├────────────────────────────────────────────────────────────┤
│ [================ CONTENT AREA ==================]         │
│ [● LIVE]                              [⏻ DISCONNECT]      │
├────────────────────────────────────────────────────────────┤
│ ┌─ Sliding Panel (when visible) ─────────────────────────┐ │
│ │ AI INSIGHT                                        [✕]  │ │
│ │ "Consider developing your bishop to control the        │ │
│ │  center squares..."                                    │ │
│ │ [Optional Image - scales to fit]                       │ │
│ │ [Progress Bar =========>                            ]  │ │
│ └────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────┘
```

### Component Breakdown

#### 1. Header Row
| Element | Description | Binding |
|---------|-------------|---------|
| **Agent Profile Icon** | 56×56 with glow effect, shows agent emoji | `SelectedAgent.Icon` |
| **Agent Name** | Bold text, e.g., "General Gaimer" | `SelectedAgent.Name` |
| **Game Icon + Title** | Small icon + process name | `CurrentTarget.ProcessName` |
| **Audio Label** | "🎤 AUDIO" header | Static |
| **IN/OUT Levels** | Volume percentages | `InputVolume`, `OutputVolume` |
| **Expand Button** | "⤢" icon, returns to MainView | `ExpandToMainViewCommand` |

#### 2. Content Area
| Element | Description |
|---------|-------------|
| **Main Area** | Placeholder for future preview or visualizer |
| **Background** | `BgPrimary` color |

#### 3. Footer Row
| Element | Description | Binding |
|---------|-------------|---------|
| **LIVE Indicator** | Green dot + "LIVE" text | Always visible when connected |
| **Disconnect Button** | Red button "⏻ DISCONNECT" | `DisconnectCommand` |

#### 4. Sliding Panel (Dropdown)
| Element | Description | Binding |
|---------|-------------|---------|
| **Panel Container** | Slides up from bottom, BgSecondary background | `SlidingPanelContent` visibility |
| **Title** | "AI INSIGHT" | Static or from content |
| **Dismiss Button** | "✕" to close panel | `DismissPanelCommand` |
| **Text Content** | AI message text, scrollable | `SlidingPanelContent.Text` |
| **Image Content** | Optional, scales to fit width | `SlidingPanelContent.ImageSource` |
| **Progress Bar** | Auto-dismiss countdown (5s default) | Timer-based |

---

## Technical Requirements

### 1. Window Resizing

Implement platform-specific window resize in `MainViewModel`:

```csharp
// When connecting and navigating to MinimalView
private async Task NavigateToMinimalViewAsync()
{
    // Resize window to compact size
    if (Application.Current?.Windows.FirstOrDefault() is Window window)
    {
        window.Width = 480;
        window.Height = 250;
    }
    
    await Shell.Current.GoToAsync("MinimalView");
}

// When expanding back to MainView
private async Task ExpandToMainViewAsync()
{
    // Resize window back to full size
    if (Application.Current?.Windows.FirstOrDefault() is Window window)
    {
        window.Width = 820;
        window.Height = 720;
    }
    
    await Shell.Current.GoToAsync("..");
}
```

**Note:** Test on both macOS and Windows - window resize APIs may behave differently.

### 2. State Sharing

The MinimalView must share state with MainView:

**Option A: Shared ViewModel (Recommended)** ✅ **IMPLEMENTED**
- Register `MainViewModel` as Singleton in DI
- Both pages use the same ViewModel instance
- State (agent, target, connection, volume) stays in sync
- MinimalViewPage binds directly to MainViewModel (not MinimalViewModel)

**Option B: State Service**
- Create `IConnectionStateService` as Singleton
- Both ViewModels read/write to this service

### 3. Sliding Panel Animation

```xaml
<!-- Panel slides up from bottom -->
<Border x:Name="SlidingPanel"
        TranslationY="200"
        IsVisible="{Binding HasPanelContent}">
    <!-- Panel content -->
</Border>
```

```csharp
// Animate panel in
await SlidingPanel.TranslateTo(0, 0, 300, Easing.CubicOut);

// Animate panel out
await SlidingPanel.TranslateTo(0, 200, 250, Easing.CubicIn);
```

### 4. Auto-Dismiss Timer

```csharp
private async Task ShowPanelAsync(SlidingPanelContent content)
{
    SlidingPanelContent = content;
    
    // Start countdown (5 seconds default)
    _dismissTimer = new Timer(async _ => 
    {
        await MainThread.InvokeOnMainThreadAsync(DismissPanel);
    }, null, content.AutoDismissMs ?? 5000, Timeout.Infinite);
}
```

---

## Files Modified (Actual Implementation)

| File | Changes |
|------|---------|
| `Views/MinimalViewPage.xaml` | Complete redesign matching spec, binds to MainViewModel |
| `Views/MinimalViewPage.xaml.cs` | Animation handling, uses MainViewModel directly from DI |
| `ViewModels/MainViewModel.cs` | Added ExpandToMainViewCommand, window resize logic, CurrentTarget property, sliding panel state |
| `MauiProgram.cs` | MainViewModel registered as Singleton for shared state |
| `Models/SlidingPanelContent.cs` | Added ImageSource, AutoDismissMs properties |

**Note:** MinimalViewModel was refactored to delegate to MainViewModel but is **not used** - MinimalViewPage binds directly to MainViewModel for simpler state sharing.

---

## Existing Code Reference

### Current MinimalViewPage Location
```
src/GaimerDesktop/GaimerDesktop/Views/MinimalViewPage.xaml
```

### Design Spec Reference
```
GAIMER/GameGhost/gaimer_spec_docs/gaimer_design_spec.md (Section 6.7)
```

### Color/Style Resources
```
src/GaimerDesktop/GaimerDesktop/Resources/Styles/Colors.xaml
src/GaimerDesktop/GaimerDesktop/Resources/Styles/Styles.xaml
```

---

## Acceptance Criteria

- [x] Window resizes to 480×250 when navigating to MinimalView ✅
- [x] Window resizes to 820×720 when expanding back to MainView ✅
- [x] Agent profile icon with glow effect displays correctly ✅
- [x] Agent name and game title/icon visible ✅
- [x] Audio IN/OUT percentages display (binding to volume properties) ✅
- [x] LIVE indicator visible when connected ✅
- [x] Disconnect button works and returns to MainView (offline) ✅
- [x] Expand button (⤢) works and returns to MainView (stays connected) ✅
- [x] Sliding panel appears when AI sends message ✅
- [x] Panel shows text content with proper styling ✅
- [x] Panel shows image content (scaled to fit) when provided ✅
- [x] Panel auto-dismisses after 5 seconds ✅
- [x] Panel can be manually dismissed with ✕ button ✅
- [x] State persists correctly between MainView and MinimalView ✅
- [x] No disconnect when returning to MainView via expand (fixes BUG-002) ✅

---

## Known Issues to Fix

### BUG-002: Auto-Disconnect on MinimalView Return
**Root Cause:** ViewModel instances not shared, navigation causes state loss
**Fix:** Use singleton MainViewModel or shared state service

### BUG-001: Audio Bars Not Animating
**Root Cause:** Mock service events not marshalled to UI thread
**Fix:** Use `MainThread.InvokeOnMainThreadAsync` in event handlers

---

## Questions for Clarification

1. Should the mini window be "always on top" for gaming visibility?
2. Should clicking the content area expand to MainView, or only the ⤢ button?
3. Should we support dragging/repositioning the mini window?

---

## Success Metrics

- MinimalView matches spec design visually
- Smooth window resize transitions
- No state loss during navigation
- AI messages display correctly in sliding panel
- Audio levels update in real-time (once BUG-001 is fixed)

---

**Estimated Effort:** 4-6 hours  
**Dependencies:** None (can start immediately)  
**Related Documents:**
- `GAIMER/GameGhost/gaimer_spec_docs/gaimer_design_spec.md`
- `GAIMER/GameGhost/BUG_FIX_LOG.md`
- `GAIMER/GameGhost/PROGRESS_LOG.md`




