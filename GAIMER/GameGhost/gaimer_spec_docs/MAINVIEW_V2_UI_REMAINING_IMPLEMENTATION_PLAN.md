# MainView V2 – Remaining UI Implementation Plan (Chat Feed + Input + Visual Polish)

**Project:** Gaimer Desktop (.NET MAUI)  
**Status:** Planning (no swap performed yet)  
**Scope:** Implement the remaining V2 UI design elements *accurately*, even before all backend features exist.  
**Primary Artifact:** `GAIMER/GameGhost/mainview-v2-design.xaml` (swap-in-place draft for `src/GaimerDesktop/GaimerDesktop/MainPage.xaml`)  

---

## Goals

- **Match the V2 UI vision** (Figma/HTML look) for the remaining items, while keeping the app functional.
- **Introduce a real chat list model** that stores *all* displayed text (user + AI) and can later store transcripts.
- **Support message types**:
  - **AI Insight**
  - **Warning**
  - **Lore**
  - **User** (user-generated messages; visually differentiated, e.g. *italics*)
- **Enable testable/mocked flows** so UI/logic can be validated before mic trigger-word and streaming details exist.

---

## Non-goals (for this plan)

- Implementing trigger-word mic activation (future)
- Implementing voice streaming UI/telemetry (future)
- Implementing “history” icons/actions (explicitly not needed)

---

## Current State (as of Dec 16, 2025)

- V2 draft exists in `GAIMER/GameGhost/mainview-v2-design.xaml` and is now **swap-in-place ready** (compilable XAML, core sections present).
- Current app has:
  - `MainViewModel.AiDisplayContent` (single payload)
  - `IConversationProvider.TextReceived` (string only)
  - `MockConversationProvider` emitting random strings
  - `MockWindowCaptureService` emitting empty frames (`byte[]` empty)
- There is **no test project** in the repo yet (no xUnit/nUnit/MSTest detected).

---

## Work Breakdown (Design Bits Left)

### 1) Chat Feed UI (multi-message list)

**Design target**
- A scrollable feed that shows:
  - message “header” (type label + timestamp)
  - message container/bubble
  - optional image inside the bubble
  - visual differentiation by message type (Warning, AI Insight, Lore, User)
- Default behavior: **focus latest** (auto-scroll to bottom when new messages arrive).

**Implementation approach**
- Replace the single `AiDisplayContent` binding in the V2 chat area with a list binding:
  - `CollectionView ItemsSource="{Binding ChatMessages}"`
  - `DataTemplateSelector` (or triggers) to style each message type.

**What can be accurate immediately**
- The full feed layout and message styles can be built now using mock data.

**Acceptance checks**
- Feed renders at least 30 messages without layout breaking.
- New messages appear and the view scrolls to the latest message.

---

### 2) Message Model + Storage (chat list)

**New model (proposed)**
- `ChatMessage`
  - `Id`
  - `Type`: `AiInsight | Warning | Lore | User`
  - `Text`
  - `ImageSource?`
  - `Timestamp`
  - (Optional) `Source`: `AI | User | System` (can be derived from Type if desired)

**MainViewModel (proposed)**
- `ObservableCollection<ChatMessage> ChatMessages`
- `ChatMessage? LatestMessage` (optional convenience)
- Commands:
  - `SendTextMessageCommand` (adds a User message immediately, then sends text to provider when supported)
  - `ClearChatCommand` (optional; if you don’t want it, skip)

**How “transcript + insights” unify**
- Everything that appears in the chat area becomes a `ChatMessage`.
- When the backend later provides transcript chunks, add them as `ChatMessage` (either `User` or `AiInsight` depending on the source).

---

### 3) Message Type Styling (Warning, AI Insight, Lore, User)

**Design target**
- **Warning**: red accent (border/left bar) + warning icon
- **AI Insight**: purple accent + “AI Insight” label
- **Lore**: cyan (or softer secondary) accent + “Lore” label
- **User**: visually distinct but not disruptive (e.g., *italics* for text, slightly different background)

**Implementation approach**
- Prefer a single `DataTemplate` + triggers (simpler) if templates are similar.
- Use `DataTemplateSelector` if Warning layout differs significantly (icon rail, thicker accent, etc.).

---

### 4) Chat Input Bar (text mode)

**Design target**
- Bottom input row inside the chat panel:
  - placeholder: “Ask Gaimer…”
  - send button
  - disabled state when offline or empty text

**Implementation approach**
- `Entry` bound to `MessageDraftText`
- send button bound to `SendTextMessageCommand`
- disable send based on:
  - `!string.IsNullOrWhiteSpace(MessageDraftText)`
  - optionally `IsConnected` (or allow offline queueing)

**Testable before backend**
- Pressing send should:
  - add a `ChatMessage(Type=User)` to `ChatMessages`
  - clear the input
  - (optionally) if provider supports text send in future, forward it

---

### 5) Preview Card Accuracy (optional polish now, real later)

**Design target**
- Show an actual preview image thumbnail with overlays (title, state).

**Implementation approach**
- Add `Image` bound to `PreviewImage` via:
  - new converter `byte[] -> ImageSource` (MemoryStream)
- For mock mode:
  - return a real embedded PNG/JPG from `MockWindowCaptureService.GeneratePlaceholderImage()`

**Testable before backend**
- Preview thumbnail renders consistently and maintains aspect ratio.

---

### 6) Bottom Bar Integration (visual)

**Design target**
- A single bottom strip containing:
  - audio area (icon + IN/OUT + visualizer)
  - provider/agent label area
  - connect/disconnect button aligned right

**Implementation approach**
- Replace separate “Audio card” + “Footer” with one container grid.

---

## Mock Data Plan (for UI validation + manual test scenarios)

### A) Extend `MockConversationProvider` to emit typed messages

**Problem today**
- Mock emits random chess phrases as plain strings. No type, no timestamp control, no deterministic scenarios.

**Proposed change (future implementation)**
- Support a deterministic “script” mode driven by config/env:
  - `MOCK_CHAT_SCRIPT=default|warnings|lore|mixed|stress`
- Emit a sequence of messages with type metadata:
  - `AI Insight`: “You’re developing pressure on the left flank…”
  - `Warning`: “HP critical. Heal now.”
  - `Lore`: “This NPC is a double agent…”
- Add predictable timings (e.g., every 2s) for repeatable UI verification.

**Interface note**
- `IConversationProvider.TextReceived` is currently `string` only. To support message types you’ll need one of:
  1) **Add a new event**: `event EventHandler<ChatMessageReceivedEventArgs> MessageReceived`
  2) Encode type in the string temporarily (e.g., `[warning] ...`) and parse in `MainViewModel` (quick, but not ideal long-term)

Recommendation: **Add a new typed event** and keep `TextReceived` for backward compatibility until all providers migrate.

---

### B) Mock “user” messages

**Goal**
- Validate UI differences (italics, subtle background, etc.)

**Proposed change (future implementation)**
- Add a “demo mode” command in `MainViewModel` (only in DEBUG or when `USE_MOCK_SERVICES=true`) that injects:
  - a `User` message on a timer or via a hidden hotkey
  - followed by an `AI Insight` response after a delay

---

### C) Mock preview images

**Problem today**
- `MockWindowCaptureService.GeneratePlaceholderImage()` returns `[]` (empty).

**Proposed change (future implementation)**
- Add one or two images in `Resources/Raw/` (e.g., `mock_preview_1.png`).
- Update `GeneratePlaceholderImage()` to load and return the bytes from the package.

---

## Testing Strategy (what’s testable before full feature support)

### 1) Manual UI сценарии (repeatable)

When `USE_MOCK_SERVICES=true` and `MOCK_CHAT_SCRIPT=mixed`:
- **Script: mixed**
  - emits 5 AI Insight + 2 Lore + 2 Warning in 60s
  - validate feed rendering + scroll-to-latest + message styles
- **Script: warnings**
  - warning burst (10 warnings)
  - validate red styling does not break layout
- **Script: stress**
  - 100 messages quickly
  - validate performance + virtualization (CollectionView)

### 2) Unit tests (recommended once a test project exists)

Add a test project later to cover:
- Chat list logic:
  - appends messages, caps list size (if you add max history)
  - “latest focus” computation
- Parsing logic (if you choose a temporary `[warning]` tag approach)
- Mock provider script runner determinism

---

## Implementation Order (recommended)

1. **ChatMessage model + ChatMessages collection** in `MainViewModel`
2. **Chat feed UI** in V2 XAML with templates/triggers for `Warning | AI Insight | Lore | User`
3. **Chat input bar** (send adds a User message; backend send optional later)
4. **MockConversationProvider scripted messages** (typed or tagged) to validate visuals
5. **Mock preview images** (optional) + preview binding
6. **Bottom bar unification** (pure UI polish; low risk)

---

## Open Questions (to decide before implementing)

1. **User message styling**: italics only, or italics + slightly different background?
2. **Warning styling**: left accent bar only, or icon rail + border?
3. **History length**: do we cap the chat list (e.g., last 200 messages) to keep memory stable?


