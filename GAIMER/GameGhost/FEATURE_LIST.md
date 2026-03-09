# Witness Desktop - Feature List

**Project:** Witness Desktop (.NET MAUI)  
**Version:** 1.0.0  
**Last Updated:** March 7, 2026

---

## Core Features

### 1. Agent Selection System
- [✅] **Agent Selection Screen** *(Redesigned Feb 23, 2026)*
  - Gaimer logo header (large, centered)
  - Bold horizontal agent cards with portrait on left, details on right
  - Full portrait images (Derek Adventurer, Leroy Chess Master)
  - Color-themed card shadows (green for Derek, amber for Leroy)
  - Agent type badge, name, specialty, description, feature grid
  - Chevron navigation with directional indicators (white/gray)
  - "SELECT YOUR GAIMER" label below cards
  - Company copyright footer (5DOF AI Studio™)
  - Centered focal layout (logo → card → label)

- [✅] **Agent Data Models** *(Updated Feb 23, 2026)*
  - Agent class with Key, Id (role), Name, PrimaryGame, Description
  - `IconImage` property for profile displays (smaller)
  - `PortraitImage` property for selection cards (full portrait)
  - `UserId` property for user-specific agent instances
  - Static Agents class with Derek (Adventurer/RPG), Leroy (Chess Master), Wasp (Chess Master)
  - Agent-specific system instructions

### 2. Main Dashboard (MainView)
- [✅] **Header Section**
  - Logo with "GAIMER" branding
  - Agent badge showing selected agent
  - Connection status indicator (Offline/Connecting/Connected)
  - Minimize button (visible when connected)
  - Settings button

- [✅] **Three-Column Layout**
  - Preview panel (left, 280px)
  - AI Insights panel (center, flexible)
  - Game Selector panel (right, 280px)

- [✅] **Preview Panel**
  - Square preview box with BgSecondary background
  - LIVE indicator when game selected
  - FPS indicator overlay
  - Game name and window title (left-aligned)

- [✅] **AI Insights Panel**
  - Header with "AI INSIGHTS" title
  - Scrollable content area
  - Empty state: "Nothing new to display"
  - Content state: Large text display (18px)
  - Proper visibility toggle between states

- [✅] **Game Selector Panel**
  - Header with "CHOOSE GAME" title
  - Refresh button
  - Scrollable game list
  - Game items with thumbnail, name, subtitle
  - Selection highlighting (cyan border)
  - Chess badge for detected chess apps

- [✅] **Audio Section**
  - Audio header with microphone icon
  - IN% and OUT% volume indicators
  - IN/OUT values update on the UI thread (stable binding)

- [✅] **Footer**
  - Agent name display
  - Version info (v1.0.0)
  - "Gemini Live API" label
  - Fixed-width Connect button (140px)
  - Spinner during connecting state
  - Button disabled until game selected

### 3. Connection System
- [✅] **Connection States**
  - Disconnected (gray, "OFFLINE")
  - Connecting (yellow, spinner)
  - Connected (green, "CONNECTED")

- [✅] **Connect Button Behavior**
  - Requires agent AND game selection to enable
  - Shows spinner during connection
  - Changes to "DISCONNECT" when connected
  - Red background when connected

- [✅] **Auto-Navigation**
  - Navigates to MinimalView on successful connection

### 4. Minimal Connected View

> **Spec Reference:** `gaimer_design_spec.md` Section 6.7  
> **Dimensions:** 960px × 350px (wide format for inline messages)  
> **Redesigned:** December 12, 2024

- [✅] **Header Row**
  - Agent profile with icon and glow effect (52×52)
  - Agent name and game process info
  - Audio level indicators (IN/OUT percentages)
  - Expand button (⤢) to return to MainView while connected

- [✅] **Message Display Area** (center, largest section)
  - Default state: "🎮 Watching your game..." placeholder
  - Active state: AI message with title header (✨ AI INSIGHT)
  - Large centered text (20pt font)
  - Optional image display
  - Tap anywhere to dismiss message
  - "tap to dismiss" hint text

- [✅] **Audio Visualizer** *(Basic Animation Implemented)*
  - Animated horizontal bars (bottom center) driven by live volume (`ActivityVolume`)
  - **Note:** Phase 4 can still upgrade visuals to SkiaSharp (optional polish)

- [✅] **Footer Row**
  - LIVE indicator (green dot + text, bottom left, no background)
  - Audio visualizer bars (bottom center)
  - Disconnect button (smaller, bottom right)

- [✅] **Window Resizing**
  - MinimalView: 960×350 (wide for messages)
  - MainView: default 1200×900, resizable (min 900×720)
  - Automatic resize on navigation
  - ⚠️ *macOS Catalyst may restore prior window size; main window remains resizable*

---

## Future Features (Planned)

### Chat Brain Architecture (Phase 02) — ✅ Complete
> **Plans:** `.planning/phases/02-chat-brain/` (6 plans executed)
> **Completed:** February 24, 2026

- [✅] **Core Models** (Plan 01)
  - SessionContext with OutGame/InGame states
  - MessageRole enum (User, Assistant, System, Proactive)
  - Timeline hierarchy: Checkpoint → EventLine → TimelineEvent
  - Generic EventOutputType (11 types) + agent-specific enums (Chess, RPG, FPS)
  - EventIconMap with no-reuse policy (16 unique icons)
  - BrainMetadata, BrainHint, ToolCallInfo, ToolDefinition models

- [✅] **Session State Machine** (Plan 02)
  - ISessionManager service with TransitionToInGame/TransitionToOutGame
  - Tool availability gating by session state (ToolDefinition.RequiredState)
  - Game connection lifecycle managed via MainViewModel

- [✅] **Brain Event Router** (Plan 03)
  - IBrainEventRouter central hub with OnScreenCapture, OnDirectMessage, OnImageAnalysis
  - Routes Brain output to Timeline, Voice, Top Strip
  - Signal-to-output type mapping via EventOutputType
  - Wired in MainViewModel (FrameCaptured → OnScreenCapture, TextReceived → OnDirectMessage/OnImageAnalysis)

- [✅] **Timeline Feed Manager** (Plan 04)
  - ITimelineFeed service with AddEvent, GetRecentEvents, CheckpointCreated event
  - Checkpoint creation on new screen captures
  - Event stacking by output type (EventLine groups same-type events)

- [✅] **Chat Prompt Builder** (Plan 05)
  - IChatPromptBuilder with BuildSystemPrompt, BuildSessionContextBlock
  - Dynamic session context injection (game type, capture, tools)
  - Tool instruction generation from ToolDefinition list

- [✅] **Timeline UI Component** (Plan 06)
  - TimelineView.xaml with CollectionView + TimelineEventTemplateSelector
  - DirectMessageBubble.xaml for user/assistant chat messages
  - ProactiveAlertView.xaml with DataTriggers for urgency (high=red, medium=yellow, low=green)
  - Auto-scroll to latest checkpoint via CheckpointCreated event
  - Integrated into MainPage.xaml (replaces flat chat CollectionView)

- [✅] **Agent Feature Gating**
  - `Agent.IsAvailable` flag for release control
  - `Agents.Available` property filters gated agents
  - Initially: Leroy (Chess) and Wasp (Chess Master) available, Derek gated

- [✅] **Event Icon Assets** (16 unique icons)
  - Generic: alert, opportunity, sage, assessment, new-game, detection, chat, video-reel, performance, history-clock, call-out
  - Chess: chessboard-eval, chess-move
  - RPG: quest, lore, item-alert

### Chat V2 MVP
- [✅] **Send Text to AI** — Implemented GMR-003 (2026-02-20)
  - `SendTextAsync` added to `IConversationProvider`; implemented in OpenAI, Mock, Gemini
  - `SendTextMessageCommand` calls provider send path; guarded when disconnected
- [✅] **Delivery State** — GMR-004: Pending/Sent/Failed on `ChatMessage`; UI metadata (…/✓/✗)
- [✅] **In-Thread Error Messaging** — GMR-005: `ErrorOccurred` → System message; 3s debounce
- [✅] **VisualReelService MVP** — GMR-006 (2026-02-20): ReelMoment model, IVisualReelService, rolling retention (time 5min + max 500), wired into MainViewModel FrameCaptured
- [✅] **SharedContextEnvelope Builder** — GMR-009, GMR-010 (2026-02-20): BrainEvent, SharedContextEnvelope, ContextAssemblyInputs models; IBrainContextService with GetContextForChatAsync/GetContextForVoiceAsync; BrainContextService MVP with deterministic token budgeting (900 voice, 1200 chat, 1600 max); truncation report; wired into MainViewModel send path; context prepended as text block (interim path)
- [ ] **Typed Backend Events** — `MessageReceived` with `ChatMessage` DTO for Warning/Lore support
- [✅] **Offline-Aware Send** — GMR-012 (2026-02-20): CanSendTextMessage (IsConnected + non-empty text); send button IsEnabled + 0.5 opacity when disabled; ChatInputPlaceholder "Connect to send messages" when offline; SendTextMessageAsync guard surfaces "Cannot send: not connected." if invoked offline
- [ ] **Retry on Send Failure** — Single retry + clear error state

### Ghost Mode — Native Floating Overlay (Phase 04) — ✅ Complete
> **Plans:** `.planning/phases/04-ghost-mode/` (3 plans executed)
> **Completed:** February 27, 2026

- [✅] **Ghost Mode Overlay (FAB + Event Cards)**
  - NSPanel-based floating overlay renders over fullscreen games
  - FAB button with agent portrait and yellow glow when connected
  - Voice/text/image event card variants with styled backgrounds
  - Pure AppKit implementation (NSView + CALayer + Core Animation)
  - GaimerGhostMode.xcframework with 16 @_cdecl exports (universal arm64+x86_64)
  - vtool binary retagging bridges macOS framework to Mac Catalyst runtime

- [✅] **Window Hide/Restore**
  - MAUI window completely hidden via NSWindow.orderOut during ghost mode
  - NSWindow.makeKeyAndOrderFront restores MAUI window on exit
  - No dock artifacts, no chrome leaks, clean transitions

- [✅] **Click-Through Transparent Areas**
  - ClickThroughHostingView with selective hit-test
  - Transparent background passes clicks through to the game
  - FAB and event cards intercept taps normally

- [✅] **Auto-Dismiss Event Cards**
  - Configurable auto-dismiss timer on event cards
  - Smooth fade-out animations via Core Animation
  - Manual dismiss via tap gesture

- [✅] **Voice/Text/Image Card Variants**
  - Text cards for direct messages and proactive alerts
  - Image cards for screen capture analysis results
  - Voice indicator for audio-based insights

- [✅] **C# Interop Layer**
  - IGhostModeService abstraction for cross-platform (macOS: NSPanel, Windows: Win32 layered window)
  - MacGhostModeService (production) + MockGhostModeService (testing)
  - DllImport P/Invoke with GCHandle callback pinning
  - Shared DllImportResolver in NativeMethods.cs for both xcframeworks

- [✅] **FAB Overlay Button on MainPage**
  - Circular agent portrait replaces Connect button in footer
  - Yellow glow ring when connected
  - Tapping toggles ghost mode entry/exit
  - MainViewModel orchestrates ghost mode toggle + event forwarding

### Windows Click-Through Overlay (PC Gamers)
- [ ] **Anchored overlay window (Windows-first)**
  - Always-on-top overlay that **tracks the target window bounds**
  - **Click-through** in stealth mode (does not steal game input)
  - **Action visibility** (fade in on messages/speaking/hotkey; fade out to stealth)
  - **Hotkey-driven** interaction (avoid mouse interference during gameplay)
  - **Initial scope:** windowed / borderless fullscreen (exclusive fullscreen later)
  - **Plan:** `GAIMER/GameGhost/gaimer_spec_docs/WINDOWS_OVERLAY_IMPLEMENTATION_PLAN.md`

- [✅] **State Sharing**
  - MainViewModel registered as Singleton
  - Shared state between MainView and MinimalView
  - Connection state persists across navigation
  - Agent and game selection preserved

- [✅] **Auto-Dismiss Timer**
  - 5-second default auto-dismiss
  - Configurable via AutoDismissMs property
  - Manual dismiss via tap gesture
  - Thread-safe UI updates

- [✅] **Navigation Commands**
  - ExpandToMainViewCommand: Returns to MainView (stays connected)
  - ToggleConnectionCommand: Disconnects and returns to MainView
  - DismissSlidingPanelCommand: Dismisses current message

#### MinimalView Implementation Summary

| Feature | Status | Notes |
|---------|--------|-------|
| Wide layout (960×350) | ✅ Done | Doubled width for better message display |
| Agent profile (52×52 glow) | ✅ Done | Header row |
| Agent name + game info | ✅ Done | Header row |
| Audio label + IN/OUT % | ✅ Done | Header row |
| Expand button | ✅ Done | Uses ⤢ character |
| **Inline message display** | ✅ Done | Replaced sliding panel |
| Large centered text (20pt) | ✅ Done | Easy to read during gameplay |
| Tap-to-dismiss | ✅ Done | Simple interaction |
| LIVE indicator (bottom) | ✅ Done | No background, subtle |
| Audio visualizer bars | ✅ Animated | Bound to live volume; Phase 4 SkiaSharp is optional polish |
| Disconnect button | ✅ Done | Smaller, bottom right |
| Auto-dismiss timer | ✅ Done | 5s default |
| State persistence | ✅ Done | Singleton MainViewModel |

### 5. Window Capture System
- [ ] **Window Enumeration**
  - List all capturable application windows
  - Filter out system processes and tiny windows
  - Display window thumbnails (64x64)
  - Refresh window list on demand
  - Windows: EnumWindows API with P/Invoke
  - macOS: CGWindowListCreateImage API

- [ ] **Window Capture**
  - Capture screenshots at 1 FPS
  - Scale images to 50% for bandwidth optimization
  - JPEG compression at 60% quality
  - Handle window closure detection
  - Handle minimized window state
  - Windows: PrintWindow API
  - macOS: CGWindowListCreateImage

- [✅] **Mock Window Selection UI**
  - App selector panel with mock targets
  - Display process name and window title
  - Show placeholder thumbnails
  - Selection state with visual feedback

### 6. Audio System

- [✅] **Microphone Capture** *(Code Complete — Awaiting Device Validation)*
  - Real-time audio capture from default microphone
  - 16kHz, 16-bit, mono PCM format
  - Volume level monitoring (RMS calculation)
  - Windows: NAudio `WaveInEvent` with 20ms buffer
  - macOS: AVAudioEngine tap on InputNode
  - Callback-based `StartRecordingAsync(Action<byte[]>)` per spec
  - Permission handling: Windows manifest + macOS Info.plist/Entitlements

- [✅] **Audio Playback** *(Code Complete — Awaiting Device Validation)*
  - Play AI response audio
  - 24kHz, 16-bit, mono PCM format
  - Interrupt capability (`InterruptPlaybackAsync`)
  - Volume level monitoring (RMS on each buffer)
  - Windows: NAudio `WaveOutEvent` with bounded ~500ms buffer + overflow logging
  - macOS: AVAudioEngine `AVAudioPlayerNode`

- [✅] **Audio Visualizer**
  - Visual representation of input/output volume
  - Baseline: XAML-bound animated bars (MainView + MinimalView)
  - Optional Phase 4: SkiaSharp-based rendering polish

- [✅] **Mock Audio Service**
  - Simulated volume events via combined `VolumeChanged` event
  - Random input/output volume generation
  - Emits non-silent 20ms PCM frames for RMS-based consumers
  - Supports callback-based capture API

### 7. Network Layer

- [✅] **Voice Provider Abstraction** *(Implemented Dec 15, 2025)*
  - `IConversationProvider` interface for provider-agnostic AI communication
  - `ConversationProviderFactory` with environment variable selection
  - Provider adapters: Gemini, OpenAI, Mock
  - Easy addition of new multimodal providers (must support text + audio + images/video)

- [✅] **Gemini Live API Integration** *(Code Complete — Testing Pending)*
  - WebSocket connection to Gemini Live API
  - Connection state management (Disconnected, Connecting, Connected, Reconnecting, Error)
  - Auto-reconnection with backoff
  - Error handling and recovery

- [✅] **OpenAI Realtime API Integration** *(Tested Dec 15, 2025)*
  - WebSocket connection to `wss://api.openai.com/v1/realtime`
  - Session creation with agent instructions (session.update)
  - Server-side VAD (Voice Activity Detection)
  - Audio streaming (input_audio_buffer.append)
  - Response handling (response.audio.delta, response.audio_transcript.delta)
  - Transcription support (user and AI speech → text)
  - ✅ MacCatalyst playback-speed fix implemented via split audio engines (BUG-008) — **Pending on-device validation**
  - ⚠️ Echo suppression improved (faster playback reduces leakage) but still **pending validation** (BUG-009)

- [✅] **Data Transmission**
  - Send audio chunks (PCM) to API (`audio/pcm;rate=16000`)
  - Send image frames (JPEG) to API (`image/jpeg`)
  - Base64 encoding for media chunks
  - Proper MIME type specification

- [✅] **Response Handling**
  - Receive audio responses from API (inlineData base64 -> PCM)
  - Handle interruption signals (`serverContent.interrupted`)
  - Parse JSON WebSocket messages (with frame assembly)
  - Extract PCM audio from responses

- [✅] **Mock Gemini Service**
  - Simulated connection state transitions
  - Mock text responses
  - Connection/disconnection simulation

- [✅] **Provider Selection** *(Environment Variable Driven)*
  - `VOICE_PROVIDER=gemini|openai|mock` for explicit selection
  - Auto-detect based on available API keys (Gemini prioritized)
  - `USE_MOCK_SERVICES=true` forces mock mode

### 8. User Interface

- [✅] **Main Dashboard**
  - Dark theme with gamer aesthetic
  - Connection status indicator
  - Preview area with game capture placeholder
  - Game selector panel
  - Connect/Disconnect button
  - AI insights display area

- [✅] **Styling**
  - Custom color palette (dark backgrounds, cyan/purple accents)
  - Custom fonts (Orbitron-Bold, Rajdhani-Regular, Rajdhani-SemiBold)
  - Gradient accents
  - Rounded corners and modern UI elements
  - Selection highlighting (cyan borders)

- [✅] **Visual Feedback**
  - Connection state colors (Green/Yellow/Gray)
  - Button disabled states (opacity 0.5)
  - Loading spinner during connection
  - Game selection highlighting

### 9. Platform Services

- [ ] **Windows Implementation**
  - Window capture service (PrintWindow)
  - Audio service (NAudio-based implementation added; needs Windows device validation)
  - Required capabilities (microphone, graphicsCapture)
  - Package.appxmanifest configuration

- [ ] **macOS Implementation**
  - Window capture service (CGWindowList)
  - Audio service (AVAudioEngine-based implementation added; needs device validation)
  - Required entitlements (audio-input, screen capture)
  - Info.plist permissions

### 10. Error Handling & Recovery

- [ ] **Connection Errors**
  - WebSocket failure handling
  - Timeout detection
  - Auto-reconnect with backoff strategy
  - User notification

- [ ] **Audio Errors**
  - Device unplugged detection
  - Permission denied handling (MacCatalyst: Info.plist + entitlement + request flow)
  - Re-initialization on error
  - User notification

- [ ] **Capture Errors**
  - Window closed detection
  - Window minimized handling
  - Capture failure recovery
  - User notification

- [ ] **Resource Cleanup**
  - Proper disposal of audio resources
  - Proper disposal of capture resources
  - WebSocket cleanup
  - Buffer clearing

### 11. Configuration & Settings

- [ ] **API Configuration**
  - Gemini API key management
  - Model selection (gemini-2.5-flash-preview-native-audio-dialog)
  - Voice selection (Fenrir)

- [ ] **System Prompt**
  - Customizable AI personality
  - Gamer-themed personality preset
  - Context-aware behavior

### 12. Asset Management *(Added Feb 23, 2026)*

- [✅] **Design Asset Repository**
  - `assets/` folder as git-tracked source of truth
  - PNG icons organized for easy access
  - Naming convention: kebab-case for source assets
  - `leroy-chessmaster-profile-icon.png` as default profile template

- [✅] **MAUI Resource Pipeline**
  - `Resources/Images/` for runtime assets
  - Underscore naming for MAUI compatibility (e.g., `derek_profile_pic.png`)
  - Assets copied and renamed when added to app

### 13. Test Coverage (Phase 10) — Complete
> **Plans:** `.planning/phases/10-integration-test-coverage/` (4 plans executed)
> **Completed:** March 3, 2026

- [✅] **Foundation Tests** (Plan 01 — 31 tests)
  - TestStubs hardened for net8.0 (Shell, Application, MainThread, ImageSource stubs)
  - VoiceConfig, SettingsService, MockAuthService, MockConversationProvider

- [✅] **Provider Factory + Auth Tests** (Plan 02 — 27 tests)
  - ConversationProviderFactory: provider selection, auto-detect, missing keys, voice gender
  - SupabaseAuthService: device validation, API key fetching, error paths

- [✅] **MainViewModel Orchestration Tests** (Plan 03 — 53 tests)
  - Constructor, lifecycle, toggle connection, stop session, send text
  - Frame capture events, connection state changes, text received events

- [✅] **Integration Sweep** (Plan 04 — 46 tests)
  - AgentSelectionViewModel: init, download flow, navigation, Stockfish lifecycle
  - Voice service guards: null agent, empty API key, disconnected state (Gemini + OpenAI)
  - Pipeline integration: FrameDiff→Brain→Channel→Router E2E, context flow

- [✅] **Total: 511/511 tests passing** (Phase 10: 157, Phase 11: 30, Phase 08 polish: 23 — zero regressions)

### 14. Agent Personality System (Phase 11) — Complete
> **Plans:** `.planning/phases/11-agent-personality-system/` (6 plans executed)
> **Completed:** March 4, 2026

- [✅] **Personality Architecture** (Plan 01)
  - Agent.cs restructured with 5 personality blocks: SoulBlock, StyleBlock, BehaviorBlock, SituationsBlock, AntiPatternsBlock
  - ToolGuidanceBlock (operational, separate from personality) + BrainPersonalityPrefix (~200 tokens)
  - ComposedPersonality computed property (cached via `??=`) with section headers

- [✅] **ChatPromptBuilder Agent-Awareness** (Plan 02)
  - IChatPromptBuilder accepts optional Agent? parameter
  - Text chat uses SOUL + BEHAVIOR blocks only, falls back to Dross CoreIdentity
  - TextMediumRules updated for agent-generic personality reference

- [✅] **Brain Service Agent-Awareness** (Plan 03)
  - AgentKey on SessionContext bridges agent selection to brain
  - OpenRouterBrainService reads AgentKey → BrainPersonalityPrefix for prompts
  - ToolExecutor stays personality-free (tools produce objective data)

- [✅] **Leroy Personality Composition** (Plan 04)
  - Composed from 64-question builder questionnaire
  - Cocky knight-obsessed tactical wildcard ("Respect the knight")
  - Compile-time const strings (LeroySoul, LeroyStyle, LeroyBehavior, LeroySituations, LeroyAntiPatterns, LeroyBrainPrefix)

- [✅] **Wasp Personality Composition** (Plan 05)
  - Distinct queen archetype contrast to Leroy
  - Composed, positional, measured ("Control the board, control the game")
  - Compile-time const strings (WaspSoul, WaspStyle, WaspBehavior, WaspSituations, WaspAntiPatterns, WaspBrainPrefix)

- [✅] **Integration Verification** (Plan 06)
  - Voice providers (Gemini/OpenAI) use ComposedPersonality instead of SystemInstruction
  - 30 new personality tests (composition, distinctiveness, tool guidance, backward compat)
  - Design files: `GAIMER/GameGhost/gaimer_spec_docs/agents/{leroy,wasp}/`
  - 488/488 total tests passing (30 new)

### 15. Brain Pipeline — End-to-End Verified (Mar 7, 2026)

- [✅] **Full Pipeline Live Test** — 10+ minutes stable against Apple Chess.app
  - Screen Capture (ScreenCaptureKit) → Brain (Claude Sonnet 4 via OpenRouter) → Timeline → Voice (Gemini Live)
  - Brain reads chess board from screenshots, delivers tactical advice in Leroy personality
  - Voice narration coexists with brain pipeline, personality consistent
  - Text chat (out-game) uses gpt-4o-mini worker model with personality

- [✅] **Brain Image Analysis** — Claude Sonnet 4 vision via OpenRouter REST
  - JPEG frames (50% scale, q60) analyzed with auto-detected MIME type (magic bytes)
  - Model ID: `anthropic/claude-sonnet-4` (corrected from dated variant)
  - dHash diff gate prevents redundant submissions
  - Results flow through Channel<BrainResult> → BrainEventRouter → Timeline/Voice/Ghost

- [✅] **Voice + Brain Coexistence**
  - Voice: Gemini Live API (direct WebSocket, low-latency bidirectional audio)
  - Brain: OpenRouter REST (vision + text/tools)
  - Both pipelines run concurrently without conflict

- [ ] **Brain Improvements (Pending)**
  - Parse window title for user's color (available in window title text)
  - Maintain move list in L2 context across captures (currently stateless)
  - FEN extraction → Stockfish validation to reduce hallucinated positions

### 16. Build & Distribution

- [✅] **Development Build**
  - macOS Catalyst build working
  - Code signing disabled for dev (`-p:EnableCodeSigning=false`)
  - Mock services for testing

- [ ] **Windows Build**
  - .NET 8.0 Windows 10+ target
  - Self-contained deployment
  - MSIX packaging
  - Code signing (Authenticode)

- [ ] **macOS Build**
  - .NET 8.0 MacCatalyst target
  - Intel (x64) support
  - Apple Silicon (ARM64) support
  - DMG/PKG packaging
  - Code signing (Apple Developer ID)

---

## Future Features (v2.0)

### Planned Enhancements
- [ ] **App Audio Capture**
  - Capture audio from target application
  - Windows: WASAPI session capture
  - macOS: ScreenCaptureKit audio capture

- [ ] **Multiple Window Support**
  - Track and capture multiple windows simultaneously
  - Multi-target selection UI

- [ ] **Hotkey Support**
  - Global keyboard shortcuts
  - Quick connect/disconnect
  - Window selection shortcut

- [ ] **Custom Voices**
  - Multiple voice options from Gemini API
  - Voice selection UI

- [ ] **Audio Device Selection** (Research Complete - December 23, 2025)
  - Independent microphone and speaker selection
  - Device enumeration (list available input/output devices)
  - Windows: Full support via NAudio device selection
  - macOS: Partial support via AVAudioSession routing
  - Bluetooth device support (with latency considerations)
  - Hot-plug detection (device connected/disconnected events)
  - **Research Document:** `GAIMER/GameGhost/AUDIO_DEVICE_SELECTION_RESEARCH.md`
  - **Estimated Effort:** 28-46 hours (Windows: 4-8hrs, macOS: 8-12hrs, Polish: 12-20hrs)

- [ ] **Session History**
  - Store conversation logs
  - Review past interactions
  - Export conversation history

---

## Feature Status Legend

- [ ] Not Started
- [🔄] In Progress
- [✅] Completed (matches spec)
- [⚠️] Partial / Spec Divergence (functional but differs from spec)
- [❌] Blocked/Cancelled

---

## Feature Statistics

| Category | Not Started | In Progress | Completed | Partial/Divergent | Total |
|----------|-------------|-------------|-----------|-------------------|-------|
| Agent System | 0 | 0 | 2 | 0 | 2 |
| Main Dashboard | 0 | 0 | 7 | 0 | 7 |
| Connection | 0 | 0 | 3 | 0 | 3 |
| Minimal View | 1 | 0 | 7 | 3 | 11 |
| Window Capture | 2 | 0 | 1 | 0 | 3 |
| Audio | 3 | 0 | 1 | 0 | 4 |
| Network | 3 | 0 | 1 | 0 | 4 |
| UI | 0 | 0 | 3 | 0 | 3 |
| Platform | 2 | 0 | 0 | 0 | 2 |
| Error Handling | 4 | 0 | 0 | 0 | 4 |
| Config | 2 | 0 | 0 | 0 | 2 |
| Build | 2 | 0 | 1 | 0 | 3 |
| **Chat Brain (Phase 02)** | **0** | **0** | **6** | **0** | **6** |
| **Ghost Mode (Phase 04)** | **0** | **0** | **7** | **0** | **7** |
| **Total** | **19** | **0** | **39** | **3** | **61** |

### Spec Divergences (Tracked)

| Feature | Original Spec | Actual Implementation | Status |
|---------|---------------|----------------------|--------|
| Main Dashboard Size | 820px × 720px | 1200px × 900px default (resizable; min 900×720) | ✅ Spec updated |
| MinimalView Size | 480px × Auto | 960px × 350px | ✅ Spec updated (wide format) |
| MinimalView Messages | Sliding panel (dropdown) | Inline centered display | ✅ Spec updated |
| Audio Visualizer | Animated bars | Animated bars bound to live volume (MainView + MinimalView); optional SkiaSharp polish later | ✅ Implemented |
| macOS Catalyst Window Sizing | - | Window may restore prior size; main is resizable | ✅ Documented workaround |

---

**Notes:**
- Features are organized by functional area
- Checkboxes indicate implementation status
- Future features are clearly marked for v2.0
- Each feature includes platform-specific implementation details where applicable

