# Gaimer - Desktop AI Gaming Companion Specification

**Version:** 1.0.0  
**Date:** December 9, 2024  
**Product Name:** Gaimer (Built on Witness Platform)  
**Target Platforms:** Windows 10+, macOS 12+

---

## Table of Contents

1. [Product Overview](#1-product-overview)
2. [Agent System](#2-agent-system)
3. [User Interface Design](#3-user-interface-design)
4. [Application Flow](#4-application-flow)
5. [Core Features](#5-core-features)
6. [UI Components Specification](#6-ui-components-specification)
7. [State Management](#7-state-management)
8. [Audio System](#8-audio-system)
9. [Game Detection & Integration](#9-game-detection--integration)
10. [Styling & Theme](#10-styling--theme)
11. [Technical Requirements](#11-technical-requirements)
12. [Implementation Phases](#12-implementation-phases)

---

## 1. Product Overview

### 1.1 Purpose

**Gaimer** is a desktop AI gaming companion built on the Witness platform. It provides real-time voice interaction and visual analysis for gamers, offering contextual commentary, strategy advice, and companionship during gaming sessions.

### 1.2 Brand Identity

| Attribute | Value |
|-----------|-------|
| Product Name | Gaimer |
| Platform | Witness |
| Tagline | "Built on the Witness platform" |
| Target Audience | PC Gamers |
| Personality | Chill, enthusiastic, knowledgeable gamer |

### 1.3 Core Value Proposition

- **AI Gaming Companion:** Voice-interactive AI that watches and comments on gameplay
- **Specialized Agents:** Domain-specific AI personalities (General gaming, Chess)
- **Non-intrusive Design:** Compact UI that doesn't obstruct gameplay
- **Real-time Analysis:** 1 FPS window capture for visual context

### 1.4 Key Differentiators

| Feature | Description |
|---------|-------------|
| Agent Selection | Choose specialized AI personalities |
| Game-Aware | Detects and adapts to specific games |
| Minimal Mode | Compact connected view for active gaming |
| Voice-First | Optimized for voice interaction |

---

## 2. Agent System

### 2.1 Agent Architecture

Gaimer supports multiple AI agent personalities, each optimized for different use cases.

```
┌─────────────────────────────────────────────────────────────┐
│                     AGENT SYSTEM                             │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────────┐    ┌─────────────────────┐        │
│  │   General Gaimer    │    │    Chess Gaimer     │        │
│  │   🎮                │    │    ♟️               │        │
│  │                     │    │                     │        │
│  │  • All games        │    │  • Chess.com        │        │
│  │  • Strategy tips    │    │  • Lichess          │        │
│  │  • Screen analysis  │    │  • Position analysis│        │
│  │  • Voice chat       │    │  • Move suggestions │        │
│  └─────────────────────┘    └─────────────────────┘        │
│                                                              │
│                    [Future Agents]                           │
│           FPS Gaimer, MOBA Gaimer, etc.                     │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Agent Definitions

#### General Purpose Gaimer

| Property | Value |
|----------|-------|
| ID | `general` |
| Name | General Gaimer |
| Icon | 🎮 |
| Color | Purple (`#a855f7`) |
| Description | All-around gaming companion for any game or genre |

**Features:**
- All games supported
- Voice chat enabled
- Screen analysis
- Strategy suggestions

**System Instruction:**
```
You are Gaimer, a highly advanced visual conversational AI designed for desktop gaming.
Your personality is that of a chill, enthusiastic, and knowledgeable gamer.
You use mild gamer slang naturally (GG, buff, nerf, clutch, etc.) but don't overdo it.

BEHAVIOR:
1. When idle (no game selected): Maintain a "lobby" or "voice chat" vibe. Ask what they're playing.
2. When viewing a game: Act like a co-op partner. Comment on gameplay, UI, graphics, strategy.
3. When viewing code/work: Offer "backseat coding" style helpful advice.
4. Keep responses concise - this is voice interaction.

You cannot hear game audio, only see screenshots. Ask the user to describe sounds if needed.
```

#### Chess Gaimer

| Property | Value |
|----------|-------|
| ID | `chess` |
| Name | Chess Gaimer |
| Icon | ♟️ |
| Color | Amber (`#f59e0b`) |
| Description | Specialized chess AI companion with grandmaster-level insights |

**Features:**
- Position analysis
- Move suggestions
- Game review
- Rating improvement tips

**System Instruction:**
```
You are Chess Gaimer, a specialized chess AI companion with grandmaster-level knowledge.
Your personality combines analytical precision with the enthusiasm of a chess streamer.

BEHAVIOR:
1. When viewing a chess position: Analyze the position, identify threats, suggest candidate moves.
2. Explain concepts clearly: Piece activity, pawn structure, king safety, tactical motifs.
3. Be encouraging: Help the player learn from mistakes without being condescending.
4. Reference famous games or players when relevant.
5. Keep responses concise but insightful.

You see screenshots at 1 FPS. Ask for clarification if the position is unclear.
Supported platforms: Chess.com, Lichess, or any chess application.
```

### 2.3 Agent Selection Data Model

```typescript
interface Agent {
  id: string;                 // 'general' | 'chess'
  name: string;               // Display name
  icon: string;               // Emoji icon
  color: string;              // Primary color hex
  glowColor: string;          // Glow effect color (rgba)
  description: string;        // Short description
  features: string[];         // Feature list
  systemInstruction: string;  // AI system prompt
  supportedGames?: string[];  // Optional: specific game support
}
```

---

## 3. User Interface Design

### 3.1 Screen Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      APPLICATION                             │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           SCREEN 1: Agent Selection                  │   │
│  │                                                      │   │
│  │  • Logo + Title                                     │   │
│  │  • Agent Cards (General, Chess)                     │   │
│  │  • Version info                                     │   │
│  └─────────────────────────────────────────────────────┘   │
│                           ↓                                  │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           SCREEN 2: Main Dashboard                   │   │
│  │                                                      │   │
│  │  • Header (Logo, Agent Badge, Status, Settings)     │   │
│  │  • Content Row (Preview + Game Selector)            │   │
│  │  • Audio Section (Visualizer + Controls)            │   │
│  │  • Footer (Agent Info, Version, Connect Button)     │   │
│  └─────────────────────────────────────────────────────┘   │
│                           ↓                                  │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           SCREEN 3: Minimal Connected View           │   │
│  │                                                      │   │
│  │  • Agent Profile + Info                             │   │
│  │  • Audio Visualizer                                 │   │
│  │  • Disconnect Button                                │   │
│  │  • Sliding Info Panel                               │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 Window Dimensions

| View | Width | Height | Notes |
|------|-------|--------|-------|
| Main Dashboard | 1200px (default) | 900px (default) | Resizable; minimum 900×720 |
| Minimal View | 960px | 350px | Wide format for message display |

> **Implementation Note (Dec 12, 2024):** 
> - Main Dashboard: 900×720 (increased from original 820px for better layout)
> - Minimal View: 960×350 (widened from 480px to display larger messages inline; fixed height for consistent UX)
> - ⚠️ **Known Issue:** macOS Catalyst sizing is in device-independent units (points) and may restore prior window size.  
>   **Update (Dec 13, 2025):** Main window is now resizable; default 1200×900; minimum 900×720.

### 3.3 Layout Grid

**Main Dashboard:**
```
┌────────────────────────────────────────────────────────────┐
│  HEADER (Auto height)                                       │
│  [Logo] [Agent Badge] [Status] .............. [Settings]   │
├────────────────────────────────────────────────────────────┤
│                                                             │
│  CONTENT ROW (Flex: 1)                                     │
│  ┌──────────────────────┐  ┌─────────────────────────┐    │
│  │                      │  │                         │    │
│  │   PREVIEW CONTAINER  │  │    GAME SELECTOR       │    │
│  │   (62% height)       │  │    (Flex: 1)           │    │
│  │                      │  │                         │    │
│  │                      │  │                         │    │
│  └──────────────────────┘  └─────────────────────────┘    │
│                                                             │
├────────────────────────────────────────────────────────────┤
│  AUDIO SECTION (Auto height)                               │
│  [Mic Icon] AUDIO  IN: 0%  OUT: 0%                        │
│  [=============== VISUALIZER CANVAS ================]      │
│  [Sliding Info Panel - Hidden by default]                  │
├────────────────────────────────────────────────────────────┤
│  FOOTER (Auto height)                                       │
│  [Agent Name] | v1.0.0 | Gemini Live API    [CONNECT]     │
└────────────────────────────────────────────────────────────┘
```

---

## 4. Application Flow

### 4.1 State Flow Diagram

```
                    ┌─────────────────┐
                    │   APP START     │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Agent Selection │
                    │    Screen       │
                    └────────┬────────┘
                             │ selectAgent(id)
                             ▼
                    ┌─────────────────┐
                    │ Main Dashboard  │◄─────────────────┐
                    │   (Offline)     │                  │
                    └────────┬────────┘                  │
                             │                           │
            ┌────────────────┼────────────────┐         │
            │                │                │         │
            ▼                ▼                ▼         │
    [Select Game]    [Change Agent]    [Connect]       │
            │                │                │         │
            │                │                ▼         │
            │                │        ┌───────────────┐ │
            │                │        │  Connecting   │ │
            │                │        └───────┬───────┘ │
            │                │                │         │
            │                │       success  │  error  │
            │                │        ┌───────┴───────┐ │
            │                │        ▼               ▼ │
            │                │  ┌───────────┐   ┌─────┴─┐
            │                │  │ Connected │   │ Error │
            │                │  │  (Online) │   └───────┘
            │                │  └─────┬─────┘
            │                │        │
            │                │        ▼
            │                │  ┌───────────────┐
            │                │  │ Minimal View  │
            │                │  │  (Optional)   │
            │                │  └───────┬───────┘
            │                │          │
            │                │          │ disconnect / expand
            │                │          │
            └────────────────┴──────────┴─────────────────┘
```

### 4.2 User Journey

1. **Launch App** → Agent Selection Screen
2. **Select Agent** → Main Dashboard (Offline state)
3. **Select Game** → Game preview shown
4. **Click Connect** → Connecting state → Connected
5. **Optional: Minimize** → Minimal Connected View
6. **Gaming Session** → AI provides voice commentary
7. **Disconnect** → Return to Dashboard (Offline)
8. **Change Agent** → Back to Agent Selection

---

## 5. Core Features

### 5.1 Feature Matrix

| Feature | General Gaimer | Chess Gaimer |
|---------|----------------|--------------|
| Voice Conversation | ✅ | ✅ |
| Window Capture | ✅ | ✅ |
| Screen Analysis | ✅ | ✅ |
| Strategy Tips | ✅ | ✅ (Chess-specific) |
| Move Suggestions | ❌ | ✅ |
| Position Analysis | ❌ | ✅ |
| Game Review | ❌ | ✅ |
| All Games Support | ✅ | ❌ (Chess only) |

### 5.2 Voice Interaction

| Property | Value |
|----------|-------|
| Input Sample Rate | 16,000 Hz |
| Output Sample Rate | 24,000 Hz |
| Format | PCM 16-bit Mono |
| Voice | Fenrir (configurable) |
| Latency Target | < 50ms |

### 5.3 Visual Analysis

| Property | Value |
|----------|-------|
| Capture Rate | 1 FPS |
| Scale Factor | 50% |
| Image Format | JPEG |
| Quality | 60% |
| Min Window Size | 100x100 px |

### 5.4 Game Detection

Automatic detection of supported games:

**Chess Games:**
- Chess.com (browser or app)
- Lichess.org (browser)
- chess24.com

**General Games:**
- Any application window
- Browser tabs
- Game launchers (Steam, Epic, etc.)

---

## 6. UI Components Specification

### 6.1 Agent Selection Screen

#### Header Section
```
┌────────────────────────────────────────┐
│         [Animated Logo Icon]           │
│                                        │
│    Select Your Gaimer                  │
│                                        │
│    gaimer - Built on Witness platform  │
│    We're refining this version...      │
└────────────────────────────────────────┘
```

#### Agent Cards

Each agent card contains:
- Icon container with glow effect
- Agent name (large heading)
- Type badge ("GAIMER")
- Description paragraph
- Feature list (4 items)
- Select button

**Card States:**
- Default: Subtle border
- Hover: Elevated with agent color border + glow
- Selected: (navigates to dashboard)

### 6.2 Main Dashboard Header

```
┌──────────────────────────────────────────────────────────────┐
│ [⚡] WITNESS   [🎮 General Gaimer ↻]   [● OFFLINE]     [⚙️] │
└──────────────────────────────────────────────────────────────┘
```

**Components:**
- Logo (Witness icon + text)
- Agent Badge (icon + name + change button)
- Connection Status Badge
- Settings Button

**Connection Badge States:**

| State | Color | Dot Animation |
|-------|-------|---------------|
| Offline | Gray | None |
| Connecting | Yellow | Blink |
| Connected | Green | Pulse |

### 6.3 Preview Container

**Empty State:**
```
┌────────────────────────────────────┐
│                                    │
│              📺                    │
│                                    │
│       No Game Selected            │
│   Choose a game from dropdown     │
│                                    │
│      [Select Application]         │
│                                    │
└────────────────────────────────────┘
```

**Active State:**
```
┌────────────────────────────────────┐
│ [● LIVE] [Chess.com]               │
│                                    │
│     [Game Window Preview]          │
│                                    │
│                                    │
│ [🎮 General]              [1 FPS] │
└────────────────────────────────────┘
```

**HUD Elements:**
- LIVE indicator (top-left)
- Target name badge (top-left)
- Agent badge (bottom-left)
- FPS indicator (bottom-right)

### 6.4 Game Selector Panel

```
┌─────────────────────────────────┐
│ [▼] 🎮 CHOOSE GAME [Chess.com] [↻] │
├─────────────────────────────────┤
│ ┌─────────────────────────────┐ │
│ │ [thumb] Chess.com           │ │
│ │         Web Browser     [♟️]│ │
│ └─────────────────────────────┘ │
│ ┌─────────────────────────────┐ │
│ │ [thumb] Discord             │ │
│ │         Voice Chat          │ │
│ └─────────────────────────────┘ │
│ ┌─────────────────────────────┐ │
│ │ [thumb] Steam               │ │
│ │         Game Launcher   [🔒]│ │
│ └─────────────────────────────┘ │
└─────────────────────────────────┘
```

**Panel Header:**
- Collapse/expand icon
- Panel icon + title
- Selected game preview (when collapsed)
- Refresh button

**App Items:**
- 48x48 thumbnail
- Process name (bold)
- Window title (muted)
- Optional badge (Chess badge, disabled lock)

**Item States:**
- Default: Card background
- Hover: Elevated + purple border
- Selected: Cyan border + inner glow
- Disabled: 40% opacity, no interaction

### 6.5 Audio Section

```
┌────────────────────────────────────────────────────────────┐
│ [🎤] AUDIO    IN: 45%    OUT: 72%                         │
├────────────────────────────────────────────────────────────┤
│ [=========== AUDIO VISUALIZER BARS ====================]  │
├────────────────────────────────────────────────────────────┤
│ ┌─ Sliding Panel (when visible) ────────────────────────┐ │
│ │ AI INSIGHT                                        [✕] │ │
│ │ The knight on e4 is well-placed, controlling key...   │ │
│ │ [Optional Image]                                      │ │
│ │ [Progress Bar =========>                            ] │ │
│ └───────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────┘
```

**Visualizer:**
- Canvas-based bar visualization
- 20 bars with gradient fill
- Reflects input/output volume
- Animated when active

**Sliding Panel:**
- Auto-dismisses after 5 seconds
- Manual dismiss button
- Can contain text and/or image
- Progress bar shows time remaining

### 6.6 Footer

```
┌────────────────────────────────────────────────────────────┐
│ General Gaimer | v1.0.0 | Gemini Live API      [⏻ CONNECT]│
└────────────────────────────────────────────────────────────┘
```

**Connect Button States:**

| State | Text | Color | Icon |
|-------|------|-------|------|
| Offline | CONNECT | Gradient (purple→cyan) | ⏻ |
| Connecting | CONNECTING... | Yellow | ⏻ (spinning) |
| Connected | DISCONNECT | Red | ⏻ |

### 6.7 Minimal Connected View

> **Updated Dec 12, 2024:** Redesigned for wider format (960×350) with inline message display.

```
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ ┌──────┐                                                🎤 AUDIO                         │
│ │ 🎮   │  General Gaimer                               IN: 45%  OUT: 72%         [⤢]   │
│ │      │  🖥️ Chrome                                                                     │
│ └──────┘                                                                                 │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│                                    ✨ AI INSIGHT                                         │
│                      "Your message is displayed here in large,                           │
│                       centered text for easy reading during gameplay"                    │
│                                   tap to dismiss                                         │
│                                                                                          │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│ ● LIVE           ▁▂▃▅▃▆▃▄▂▃▅▃ (audio visualizer)                    [⏻ DISCONNECT]     │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

**Layout (Top to Bottom):**

1. **Header Row**
   - Agent profile icon (52×52 with purple glow)
   - Agent name + game process info
   - Audio label with IN/OUT percentages
   - Expand button (⤢) to return to MainView

2. **Message Display Area** (center, largest section)
   - Default: "🎮 Watching your game..." placeholder
   - Active: AI message with title, large centered text (20pt)
   - Tap anywhere to dismiss message
   - Auto-dismiss after 5 seconds

3. **Footer Row**
   - LIVE indicator (green dot + text)
   - Audio visualizer bars (centered, horizontal)
   - Disconnect button (smaller, red)

**Components:**
- Agent profile (52×52 with glow)
- Agent name + game info
- Audio label + volume levels (header)
- Expand button (returns to MainView while connected)
- **Message display area** (inline, no sliding panel)
- Audio visualizer bars (bottom center)
- LIVE indicator (bottom left)
- Disconnect button (bottom right)

---

## 7. State Management

### 7.1 Application State

```typescript
interface AppState {
  // Current screen
  currentScreen: 'agent-selection' | 'dashboard' | 'minimal';
  
  // Agent
  selectedAgent: Agent | null;
  
  // Connection
  connectionState: ConnectionState;
  error: string | null;
  
  // Game selection
  captureTargets: CaptureTarget[];
  selectedTarget: CaptureTarget | null;
  isCapturing: boolean;
  
  // Audio
  inputVolume: number;   // 0-1
  outputVolume: number;  // 0-1
  
  // UI
  gameSelectorCollapsed: boolean;
  slidingPanelContent: SlidingPanelContent | null;
}

enum ConnectionState {
  DISCONNECTED = 'DISCONNECTED',
  CONNECTING = 'CONNECTING',
  CONNECTED = 'CONNECTED',
  ERROR = 'ERROR'
}

interface SlidingPanelContent {
  title: string;
  text: string;
  imageUrl?: string;
  autoDismissMs?: number;
}
```

### 7.2 State Transitions

```
┌──────────────┐  selectAgent()   ┌─────────────┐
│    null      │ ───────────────► │   Agent     │
│  (no agent)  │                  │  selected   │
└──────────────┘                  └─────────────┘

┌──────────────┐  connect()       ┌─────────────┐
│ DISCONNECTED │ ───────────────► │ CONNECTING  │
└──────────────┘                  └──────┬──────┘
       ▲                                 │
       │         ┌───────────────────────┼───────────────────┐
       │         │ onopen()              │ onerror()         │
       │         ▼                       ▼                   │
       │  ┌─────────────┐         ┌─────────────┐           │
       │  │  CONNECTED  │         │    ERROR    │           │
       │  └──────┬──────┘         └─────────────┘           │
       │         │                                          │
       │         │ disconnect() / onclose()                 │
       └─────────┴──────────────────────────────────────────┘
```

---

## 8. Audio System

### 8.1 Audio Pipeline

```
┌─────────────────────────────────────────────────────────────┐
│                    AUDIO SYSTEM                              │
│                                                              │
│  INPUT (User → AI)                                          │
│  ┌──────────┐    ┌───────────┐    ┌──────────┐             │
│  │Microphone│ →  │16kHz PCM  │ →  │ WebSocket│ → Gemini    │
│  │          │    │Processing │    │          │             │
│  └──────────┘    └───────────┘    └──────────┘             │
│                        ↓                                    │
│                  [Volume Meter]                             │
│                                                              │
│  OUTPUT (AI → User)                                         │
│  Gemini → ┌──────────┐    ┌───────────┐    ┌──────────┐   │
│           │ WebSocket│ →  │24kHz PCM  │ →  │ Speakers │   │
│           │          │    │Playback   │    │          │   │
│           └──────────┘    └───────────┘    └──────────┘   │
│                                ↓                           │
│                          [Volume Meter]                    │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### 8.2 Volume Visualization

Volume meters display real-time RMS levels:

```typescript
// Calculate RMS volume (0-1 range)
function calculateRMS(samples: Float32Array): number {
  let sum = 0;
  for (let i = 0; i < samples.length; i++) {
    sum += samples[i] * samples[i];
  }
  return Math.sqrt(sum / samples.length);
}

// Display as percentage
const displayPercent = Math.round(rmsVolume * 100);
```

### 8.3 Audio Interruption

When user speaks during AI response:
1. API sends `interrupted: true`
2. Stop all playing audio immediately
3. Clear audio buffer queue
4. Reset scheduling timer

---

## 9. Game Detection & Integration

### 9.1 Detection Flow

```
┌───────────────────┐
│  Enumerate Windows │
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│  Filter Visible   │
│  (>100x100 px)    │
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│  Get Process Info │
│  + Window Title   │
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│  Match Known Games│
│  (if Chess agent) │
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│  Generate Thumbs  │
│  (64x64 JPEG)     │
└───────────────────┘
```

### 9.2 Chess Game Detection

For Chess Gaimer, detect chess applications:

```typescript
const CHESS_PATTERNS = [
  { process: 'chrome', title: /chess\.com/i, badge: '♟️ Chess.com' },
  { process: 'firefox', title: /chess\.com/i, badge: '♟️ Chess.com' },
  { process: 'chrome', title: /lichess/i, badge: '♟️ Lichess' },
  { process: 'Chess.com', title: /.*/i, badge: '♟️ Chess.com App' },
];

function detectChessGame(target: CaptureTarget): ChessBadge | null {
  for (const pattern of CHESS_PATTERNS) {
    if (target.processName.includes(pattern.process) && 
        pattern.title.test(target.windowTitle)) {
      return pattern.badge;
    }
  }
  return null;
}
```

### 9.3 Game-Specific UI

When Chess Gaimer detects a chess game:
- Show chess badge on game item
- Prioritize in list
- Enable chess-specific AI features

---

## 10. Styling & Theme

### 10.1 CSS Variables

```css
:root {
  /* Agent Colors */
  --agent-general: #a855f7;
  --agent-general-glow: rgba(168, 85, 247, 0.3);
  --agent-chess: #f59e0b;
  --agent-chess-glow: rgba(245, 158, 11, 0.3);
  
  /* Background Colors */
  --bg-primary: #050505;
  --bg-secondary: #0f172a;
  --bg-card: #1e293b;
  --bg-card-hover: #334155;
  
  /* Accent Colors */
  --accent-cyan: #06b6d4;
  --accent-purple: #a855f7;
  --accent-blue: #3b82f6;
  --accent-green: #22c55e;
  --accent-red: #ef4444;
  --accent-yellow: #eab308;
  
  /* Text Colors */
  --text-primary: #e0e0e0;
  --text-secondary: #64748b;
  --text-muted: #475569;
  
  /* Gradients */
  --gradient-accent: linear-gradient(135deg, #a855f7, #06b6d4);
  
  /* Spacing */
  --spacing-xs: 4px;
  --spacing-sm: 8px;
  --spacing-md: 16px;
  --spacing-lg: 24px;
  --spacing-xl: 32px;
  
  /* Border Radius */
  --radius-sm: 6px;
  --radius-md: 12px;
  --radius-lg: 16px;
  
  /* Transitions */
  --transition-fast: 150ms ease;
  --transition-normal: 250ms ease;
}
```

### 10.2 Typography

| Element | Font Family | Size | Weight |
|---------|-------------|------|--------|
| Logo | Orbitron | 28px | 700 |
| Headings | Orbitron | 24px | 700 |
| Panel Titles | Orbitron | 9-12px | 700 |
| Body Text | Rajdhani | 14px | 400-600 |
| Labels | Rajdhani | 12px | 600 |
| Status | Rajdhani | 14px | 600 |

### 10.3 Animations

| Animation | Duration | Easing | Usage |
|-----------|----------|--------|-------|
| Pulse | 2s | Infinite | Connected dot |
| Blink | 1s | Infinite | Connecting dot |
| Float | 3s | ease-in-out | Logo on selection |
| Slide In | 0.4s | cubic-bezier | Sliding panel |
| Progress | 5s | linear | Panel auto-dismiss |

### 10.4 Glow Effects

```css
/* Cyan glow (logo, connected elements) */
filter: drop-shadow(0 0 8px rgba(6, 182, 212, 0.5));
text-shadow: 0 0 20px rgba(6, 182, 212, 0.5);
box-shadow: 0 0 20px rgba(6, 182, 212, 0.3);

/* Agent-specific glows */
.agent-general { box-shadow: 0 10px 40px rgba(168, 85, 247, 0.3); }
.agent-chess { box-shadow: 0 10px 40px rgba(245, 158, 11, 0.3); }
```

---

## 11. Technical Requirements

### 11.1 Platform Requirements

| Platform | Minimum Version | Architecture |
|----------|-----------------|--------------|
| Windows | 10 (1903+) | x64, ARM64 |
| macOS | 12.0+ | Intel, Apple Silicon |

### 11.2 API Requirements

| Service | Requirement |
|---------|-------------|
| Gemini API | Valid API key |
| Model | gemini-2.5-flash-preview-native-audio-dialog |
| Connection | WebSocket (wss://) |

### 11.3 Permissions

**Windows:**
- Microphone access
- Screen capture (Graphics Capture API)
- Internet access

**macOS:**
- Microphone access (NSMicrophoneUsageDescription)
- Screen recording (NSScreenCaptureUsageDescription)
- Internet access

### 11.4 Performance Targets

| Metric | Target |
|--------|--------|
| Audio latency (input) | < 50ms |
| Audio latency (output) | < 50ms |
| Window capture rate | 1 FPS |
| Memory footprint | < 150MB |
| CPU usage (idle) | < 5% |
| CPU usage (active) | < 15% |

---

## 12. Implementation Phases

### Phase 1: Core UI (Week 1)
- [ ] Agent selection screen
- [ ] Main dashboard layout
- [ ] Game selector panel
- [ ] Audio section UI
- [ ] Footer with connect button

### Phase 2: Agent System (Week 1-2)
- [ ] Agent data models
- [ ] Agent switching logic
- [ ] Agent-specific system prompts
- [ ] Agent badges throughout UI

### Phase 3: Audio Integration (Week 2)
- [ ] Microphone capture
- [ ] Audio playback
- [ ] Volume metering
- [ ] Audio visualizer

### Phase 4: Window Capture (Week 2-3)
- [ ] Window enumeration
- [ ] Screenshot capture
- [ ] Preview display
- [ ] Game detection

### Phase 5: API Integration (Week 3)
- [ ] WebSocket connection
- [ ] Audio streaming
- [ ] Image streaming
- [ ] Response handling

### Phase 6: Minimal View (Week 3-4)
- [ ] Compact connected UI
- [ ] View switching logic
- [ ] Sliding info panel

### Phase 7: Polish (Week 4)
- [ ] Animations and transitions
- [ ] Error handling
- [ ] Edge cases
- [ ] Performance optimization

---

## Appendix A: Future Agent Ideas

| Agent | Icon | Specialty |
|-------|------|-----------|
| FPS Gaimer | 🎯 | Aim tips, callouts, map knowledge |
| MOBA Gaimer | ⚔️ | Lane management, builds, team fights |
| Speedrun Gaimer | ⏱️ | Route optimization, glitch analysis |
| Retro Gaimer | 👾 | Classic game knowledge |
| Stream Buddy | 📺 | Chat interaction, alerts |

---

## Appendix B: Reference Mockup

The UI design is based on the interactive HTML/CSS/JS mockup located at:
```
GaimerDesktop/ui-mockup/
├── index.html
├── styles.css
├── app.js
└── README.md
```

---

**Document Version History**

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | Dec 9, 2024 | Initial specification based on UI mockup |

---

*This document specifies the Gaimer desktop application, a gaming-focused AI companion built on the Witness platform. For the canonical Witness platform specification, see `SPEC_DOCUMENT.md`.*

