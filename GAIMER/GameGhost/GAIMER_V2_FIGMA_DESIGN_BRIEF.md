# Gaimer V2: Figma Design Brief & Component Checklist

This document serves as the source of truth for creating the **Gaimer V2 MainView** design in Figma. Use these specifications to build the components that will eventually be translated into .NET MAUI XAML.

---

## 1. Visual Identity & Tokens

### Color Palette (Gaimer Dark)
*   **BgPrimary:** `#130C1A` (Main page background)
*   **BgSecondary:** `#1F152A` (Sidebar & Header background)
*   **BgCard:** `#1A1225` (Chat bubbles & Panel backgrounds)
*   **BgCardHover:** `#2A1F3A` (Hover states)
*   **AccentPurple:** `#7F13EC` (Primary brand color / Connect button)
*   **AccentPurpleGlow:** `rgba(127, 19, 236, 0.3)`
*   **AccentCyan:** `#AD92C9` (Secondary accent / AI status)
*   **Status Colors:**
    *   **Success/Live:** `#10B981` (Green)
    *   **Warning/Alert:** `#EF4444` (Red)
    *   **Pending/Connecting:** `#F59E0B` (Yellow)

### Typography
*   **Headers:** `Orbitron` (Bold) - Used for "GAIMER" logo and panel titles (e.g., "LIVE PREVIEW").
*   **UI/Body:** `Rajdhani` (Regular, SemiBold, Bold) - Used for all chat text, labels, and button text.

---

## 2. Layout Architecture
**Canvas Size:** 1200px x 900px (Desktop)

### A. Header (Top Bar)
*   **Logo:** ⚡ GAIMER (Orbitron Bold, AccentPurple icon).
*   **Agent Badge:** Rounded border card showing "🤖 [Agent Name]" with a refresh/change icon.
*   **Connection Status:** Pill-shaped badge with a pulsing dot (Green for Connected, Gray for Offline).
*   **Action Icons:** Minimize (🗕) and Settings (⚙️) aligned to the far right.

### B. Sidebar (Fixed 300px Left)
*   **Live Preview Card:** 
    *   Square/Rectangular area for game capture.
    *   Overlay: "LIVE" badge (Red dot) in top-right.
    *   Overlay: "1 FPS" label in bottom-right.
*   **Active Application List:**
    *   List items for detected windows.
    *   Selected state: Purple border + "Connected • Running" sub-label.
    *   Thumbnail icons (40x40 rounded).

### C. Main Chat Area (Flexible Right)
*   **Chat Header:** 
    *   "AI Companion" title.
    *   Status: "Session Active • Monitoring".
    *   Icons: History (🕐) and Clear (🗑️).
*   **The Chat Feed (Scrollable):**
    *   **Message Types (Crucial):**
        1.  **AI Insight:** Standard bubble with Purple accent bar.
        2.  **Warning:** Red border/accent + ⚠️ icon. High visibility.
        3.  **Lore:** Cyan accent. Softer, informative vibe.
        4.  **User Message:** Italics text, slightly darker background. Represents what the AI "heard".
*   **Chat Input Bar (Bottom):**
    *   Rounded input field: "Ask Gaimer..."
    *   Send icon (Arrow/Paper plane).

---

## 3. Interactive Component States

### The Connect Button
1.  **Disconnected:** Background: `AccentPurple`, Text: "CONNECT".
2.  **Connecting:** Background: `AccentPurple`, Text: "...", Show Spinner.
3.  **Connected:** Background: `AccentRed`, Text: "DISCONNECT".

### Audio Visualizer
*   A row of 12-20 vertical bars.
*   Color: `AccentPurple` or `AccentCyan`.
*   Design 3 frames of varying heights to represent "Active" state.

---

## 4. Design Goals for V2
*   **Native Desktop Feel:** Avoid looking like a mobile app stretched out. Use subtle borders (`#ffffff0d`) and deep shadows.
*   **Readability:** Chat text should be large (16px-18px) for quick glances while gaming.
*   **Non-Intrusive:** The sidebar should feel secondary to the chat insights.

---
**Next Step:** Once the Figma design is ready, provide the URL to pull these tokens and layouts into the .NET MAUI project.
