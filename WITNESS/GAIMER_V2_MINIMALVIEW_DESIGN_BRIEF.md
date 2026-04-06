# Gaimer V2: MinimalView Design Brief & Spec

This document defines the **MinimalView V2** design requirements for Figma. This view is the "active gaming" mode—a compact, non-intrusive overlay that provides real-time AI insights without blocking the game.

---

## 1. Visual Identity & Layout
**Canvas Size:** 960px x 350px (Wide Desktop Format)

### A. Background & Container
*   **Background:** `#1F152A` (BgSecondary) with a subtle border `#ffffff0d`.
*   **Corner Radius:** 16px.
*   **Padding:** 16px internal padding.

---

## 2. Component Breakdown

### A. Header Row (Top)
*   **Agent Profile (Left):** 
    *   52x52 rounded border card.
    *   Glow Effect: `AccentPurple` (Shadow radius 16px, 0.4 opacity).
    *   Content: Selected Agent Icon (e.g., 🤖).
*   **Session Info:**
    *   Agent Name (Rajdhani SemiBold, 18px, `TextPrimary`).
    *   Active Game (Rajdhani Regular, 12px, `TextSecondary`) with a small 🖥️ icon.
*   **Audio Monitor:**
    *   "🎤 AUDIO" label (Orbitron Bold, 8px, `AccentCyan`).
    *   IN/OUT levels (Rajdhani Regular, 10px, `TextSecondary`).
*   **Expand Button (Right):**
    *   Icon: `⤢` (40x40 card button).
    *   Action: Returns to MainView.

### B. Message Display Area (Center - Dynamic)
This is the core of the MinimalView. It has two states:

**State 1: Idle/Ready**
*   Icon: 🎮 (32px, 0.4 opacity).
*   Text: "Watching your game..." (Rajdhani Regular, 16px, `TextSecondary`).

**State 2: AI Insight (Active)**
*   **Header:** ✨ AI INSIGHT (Orbitron Bold, 11px, `AccentCyan`).
*   **Message Text:** Large, centered text (Rajdhani Regular, 20px, `TextPrimary`).
*   **Optional Image:** Centered thumbnail (Max height 120px).
*   **Interaction:** "tap to dismiss" hint (10px, 0.5 opacity).

### C. Footer Row (Bottom)
*   **LIVE Indicator (Left):** Green pulsing dot + "LIVE" text (Rajdhani SemiBold, 12px, `AccentGreen`).
*   **Audio Visualizer (Center):** 
    *   12 horizontal bars (4px width).
    *   Color: `AccentCyan`.
    *   Height should animate/vary based on activity.
*   **Disconnect Button (Right):**
    *   Text: "⏻ DISCONNECT".
    *   Style: Red background, compact (Rajdhani SemiBold, 11px).

---

## 3. Interaction Design
*   **Inline Messages:** Unlike V1, V2 uses **inline message display** in the center area rather than a sliding dropdown.
*   **Auto-Dismiss:** Messages should visually "fade in" and automatically dismiss after 5 seconds.
*   **Transparency:** The background should be slightly translucent (`0.9` opacity) to feel more like a HUD.

---

## 4. Design Goals
*   **HUD Aesthetic:** Should look like a high-end gaming peripheral (e.g., Elgato or Discord overlay).
*   **Extreme Readability:** The 20px message text is designed to be readable in peripheral vision while focusing on a game.
*   **Compactness:** Must provide all critical info (Who is watching? What game? Is the mic working? What did they say?) in a 350px height.

---
**Next Step:** Build this 960x350 frame in Figma using the Gaimer Dark color palette defined in the MainView brief.
