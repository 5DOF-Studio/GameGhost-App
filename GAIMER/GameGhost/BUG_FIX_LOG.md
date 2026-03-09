# Witness Desktop - Bug Fix Log

**Project:** Witness Desktop (.NET MAUI)  
**Version:** 1.0.0  
**Last Updated:** December 14, 2025

---

## Bug Tracking Format

Each bug entry follows this structure:
- **ID:** Unique identifier (e.g., BUG-001)
- **Date Reported:** When the bug was discovered
- **Date Fixed:** When the bug was resolved
- **Severity:** Critical / High / Medium / Low
- **Status:** Open / In Progress / Fixed / Won't Fix / Duplicate
- **Component:** Affected system component
- **Description:** Detailed description of the issue
- **Steps to Reproduce:** How to trigger the bug
- **Expected Behavior:** What should happen
- **Actual Behavior:** What actually happens
- **Root Cause:** Analysis of why the bug occurred
- **Fix:** Description of the solution
- **Files Changed:** List of files modified
- **Tested:** Verification steps taken

---

## Bug Entries

### BUG-XXX: MainPage.xaml XAML compile errors (GMR-009/010 stabilization)
- **Date Reported:** February 20, 2026 (during GMR-009/010 build)
- **Date Fixed:** February 20, 2026
- **Severity:** Critical (blocking)
- **Status:** ✅ Fixed
- **Component:** MainPage.xaml
- **Description:** Two XAML compile errors blocking build: (1) ItemsUpdatingScrollMode="KeepLastItem" — invalid enum value; (2) CollectionView Padding="24" — CollectionView/ItemsView does not support Padding.
- **Root Cause:** KeepLastItem is not a valid ItemsUpdatingScrollMode enum member (correct: KeepLastItemInView). CollectionView inherits from ItemsView/View, not Layout, so it has no Padding property.
- **Fix:** Changed ItemsUpdatingScrollMode to KeepLastItemInView; replaced Padding with Margin="24" on CollectionView. Also added ByteArrayToImageSourceConverter to App.xaml (MainPage referenced it but it was not registered).
- **Files Changed:** `MainPage.xaml`, `App.xaml`

### BUG-XXX: AddSystemMessage debounce fields missing (compilation error)
- **Date Reported:** February 20, 2026 (during GMR-009/010 build)
- **Date Fixed:** February 20, 2026
- **Severity:** Critical (blocking)
- **Status:** ✅ Fixed
- **Component:** MainViewModel / AddSystemMessage
- **Description:** GMR-005 added AddSystemMessage with debounce using `_lastSystemError` and `_lastSystemErrorAt`, but these fields were never declared, causing CS0103 compilation errors.
- **Root Cause:** Fields omitted when AddSystemMessage debounce logic was added.
- **Fix:** Added `private string _lastSystemError` and `private DateTime _lastSystemErrorAt` to MainViewModel.
- **Files Changed:** `ViewModels/MainViewModel.cs`

### BUG-001: Audio Bars Not Animating
- **Date Reported:** December 10, 2024
- **Date Fixed:** December 13, 2025
- **Severity:** Medium
- **Status:** ✅ Fixed
- **Component:** Audio System / UI Binding
- **Description:** The IN% and OUT% audio level indicators in the main view and minimal view do not animate or update when connected, despite the MockAudioService generating volume events.
- **Steps to Reproduce:**
  1. Select an agent
  2. Select a game from the list
  3. Click Connect
  4. Observe the AUDIO section - IN% and OUT% remain static
- **Expected Behavior:** IN% should fluctuate between 10-70% simulating microphone input; OUT% should pulse periodically simulating AI speech.
- **Actual Behavior:** Both values remain at 0% or do not update visually.
- **Root Cause:**
  - MinimalView “audio bars” were implemented as **static placeholder BoxViews** with fixed `HeightRequest` values (no binding/animation).
  - Volume events can arrive on background threads; ViewModel properties were being set off the UI thread, preventing reliable UI updates.
- **Fix:**
  - Marshal audio volume event handlers onto the UI thread (`MainThread.BeginInvokeOnMainThread`).
  - Add `ActivityVolume` (max of IN/OUT) to drive a simple visualizer.
  - Bind MinimalView bar heights to `ActivityVolume` using a value converter with per-bar variation.
  - Adjust `MockAudioService` to emit **non-silent PCM16 frames** (20ms @ 16kHz) so any RMS-based consumer won’t flatline.
- **Files Changed:**
  - `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs`
  - `src/GaimerDesktop/GaimerDesktop/Views/MinimalViewPage.xaml`
  - `src/GaimerDesktop/GaimerDesktop/Utilities/ValueConverters.cs`
  - `src/GaimerDesktop/GaimerDesktop/App.xaml`
  - `src/GaimerDesktop/GaimerDesktop/Services/MockAudioService.cs`
- **Tested:**
  - Connect with Mock services; observe IN/OUT % updating and MinimalView bars changing height continuously.

---

### BUG-002: Auto-Disconnect on Return from MinimalView
- **Date Reported:** December 10, 2024
- **Date Fixed:** December 12, 2024
- **Severity:** High
- **Status:** ✅ Fixed
- **Component:** Navigation / State Management
- **Description:** When returning from MinimalView to MainView (via expand button), the connection automatically disconnects. The audio count/timer appears to continue running causing state inconsistency.
- **Steps to Reproduce:**
  1. Select agent and game
  2. Click Connect → Auto-navigates to MinimalView
  3. Click Expand button (⤢) to return to MainView
  4. Connection state shows as Disconnected
- **Expected Behavior:** Connection should remain active when expanding from MinimalView back to MainView.
- **Actual Behavior:** Connection terminates upon navigation back to MainView.
- **Root Cause:** ViewModel instances not shared between pages. Each navigation created new ViewModel instances, losing connection state. MinimalViewModel had its own connection management that conflicted with MainViewModel.
- **Fix:** 
  1. Made MainViewModel a Singleton in MauiProgram.cs (`services.AddSingleton<MainViewModel>()`)
  2. MinimalViewPage now uses MainViewModel directly instead of MinimalViewModel for binding
  3. Added `ExpandToMainViewCommand` in MainViewModel that properly navigates back without disconnecting
  4. Window resize logic centralized in MainViewModel
  5. Sliding panel and auto-dismiss timer handled in MinimalViewPage.xaml.cs
  6. Removed MinimalViewModel DI registration (unused - page binds directly to MainViewModel)
- **Files Changed:**
  - `MauiProgram.cs` - Changed MainViewModel to Singleton, removed MinimalViewModel registration
  - `ViewModels/MainViewModel.cs` - Added ExpandToMainViewCommand, window resize logic, CurrentTarget property
  - `ViewModels/MinimalViewModel.cs` - Refactored to delegate to MainViewModel (kept but unused)
  - `Views/MinimalViewPage.xaml` - Updated to use MainViewModel (`x:DataType="vm:MainViewModel"`)
  - `Views/MinimalViewPage.xaml.cs` - Gets MainViewModel from DI, handles panel animations
- **Tested:** Navigation from MinimalView to MainView preserves connection state. Expand button returns to MainView without disconnect.

---

### BUG-003: Main Window Size Not Honoring Specification (macOS Catalyst)
- **Date Reported:** December 13, 2025
- **Date Fixed:** December 13, 2025
- **Severity:** Medium
- **Status:** ✅ Fixed (Workaround)
- **Component:** Window Management / macOS Catalyst
- **Description:** On macOS Catalyst, the app window may open smaller than the fixed size configured in code, and attempts to hard-lock the size (Min=Max) can make the window non-resizable while still not guaranteeing the desired apparent size.
- **Steps to Reproduce:**
  1. Launch the app on macOS Catalyst
  2. Observe window size on first screen
- **Expected Behavior:** Main window opens at the desired spec size and is usable on macOS.
- **Actual Behavior:** Apparent size can differ due to platform scaling and/or restored window state; fixed-size enforcement did not resolve it.
- **Root Cause (Working Hypothesis):** macOS Catalyst restores window state and applies platform sizing/scaling after MAUI `CreateWindow()` runs; MAUI Width/Height values are device-independent units, not literal pixels, so “900×720” may not match visual expectations on all displays.
- **Fix (Workaround):**
  1. Make the Main window **resizable** (remove Min=Max lock)
  2. Start the app at a **larger default size** (1200×900) with a **minimum floor** (900×720)
  3. Keep MinimalView sizing fixed (960×350) for the connected mini mode
- **Files Changed:**
  - `src/GaimerDesktop/GaimerDesktop/App.xaml.cs`
  - `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs`
- **Tested:** Verified on macOS: main window can be resized by dragging edges; default size is visibly larger; app remains usable.

---

### BUG-004: MinimalView Not Auto-Opening After Connect (Race Condition)
- **Date Reported:** December 14, 2025
- **Date Fixed:** December 14, 2025
- **Severity:** Medium
- **Status:** ✅ Fixed
- **Component:** Navigation / Connection Flow
- **Description:** After clicking CONNECT, the app sometimes stayed on the Main Dashboard instead of auto-switching to MinimalView, even though the connection badge showed Connected.
- **Steps to Reproduce:**
  1. Launch the app
  2. Select agent + game
  3. Click CONNECT
  4. Observe that the connection badge transitions to Connected, but the route does not navigate to MinimalView
- **Expected Behavior:** On successful connection, the app should reliably resize + navigate to MinimalView.
- **Actual Behavior:** Navigation sometimes did not occur.
- **Root Cause:** A race between `ConnectAsync()` completion and the async `ConnectionStateChanged` event handler updating `ConnectionState`. `ToggleConnectionAsync()` was checking `ConnectionState` immediately after `ConnectAsync()` returned, before the UI-thread state update fired.
- **Fix:**
  - Introduced a “navigate-on-connected” flag set at the start of connect.
  - Navigation is now triggered when `ConnectionStateChanged` reaches `Connected`, eliminating the race.
  - Added a visible backend indicator (`GeminiBackendText`) and startup DI log lines to confirm Mock vs Live at runtime.
- **Files Changed:**
  - `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs`
  - `src/GaimerDesktop/GaimerDesktop/MainPage.xaml`
  - `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs`
- **Tested:** Relaunched on macOS; CONNECT consistently triggers MinimalView navigation. Startup logs show selected backend.

---

### BUG-005: MacCatalyst Microphone Capture Not Producing Audio Frames (No "Hot Mic" Indicator)
- **ID:** BUG-005
- **Date Reported:** December 14, 2025
- **Date Fixed:** December 14, 2025
- **Severity:** High
- **Status:** ✅ Fixed
- **Component:** Audio System / MacCatalyst (AVAudioEngine tap) + Integration (Mic → Gemini)
- **Description:** On macOS (MacCatalyst), user can connect to Gemini Live, but microphone capture appears inactive (no system mic-in-use indicator; user reports AI cannot hear them). Logs show mic authorization is `Authorized` and recording start is requested, but capture frames are not observed.
- **Current Observations (from logs):**
  - App is using `GeminiLiveService` (not mock): `[Gaimer][DI] IGeminiService=GeminiLiveService (USE_MOCK_SERVICES=False, apiKeyPresent=true)`
  - `StartRecordingAsync` is invoked after CONNECT.
  - `AVAuthorizationStatus` returns `Authorized`, so **no permission prompt is expected**.
  - Logging previously stalled at/around `InstallTapOnBus(...)` with no subsequent "engine started" or "captured frame" logs.
  - Latest logs show `InstallTapOnBus` throws: **`Input HW format is invalid`** when forcing tap format `PCMInt16/16k/mono`.
- **Steps to Reproduce:**
  1. Launch app on macOS (MacCatalyst)
  2. Ensure `.env` contains `USE_MOCK_SERVICES=false` and a valid `GEMINI_API_KEY`
  3. Select an agent + game
  4. Click CONNECT
  5. Speak; observe no reliable evidence the mic is capturing (no orange mic dot / no audio frames logged)
- **Expected Behavior:**
  - After CONNECT, mic capture starts, audio frames are produced continuously, and frames are sent to Gemini (`SendAudioAsync`).
  - macOS shows "mic in use" indicator (OS version dependent).
- **Actual Behavior:**
  - Connection appears live, but mic capture does not appear to produce frames; user cannot confirm AI hears them.

#### Root Cause Analysis (BUG-005)

**Confirmed Root Cause:** The code was accessing `inputNode.GetBusOutputFormat(0)` **before** the AVAudioSession was configured and activated.

On MacCatalyst (which uses the iOS-style audio stack), the AVAudioEngine's input node format is **invalid** (`sampleRate=0`, `channelCount=0`) until:
1. **AVAudioSession** is configured with the correct category (e.g., `PlayAndRecord`)
2. **AVAudioSession** is activated via `SetActive(true)`
3. The audio hardware is fully initialized

**Why This Happens on MacCatalyst:**
- MacCatalyst is essentially an iPad app running on macOS
- It uses the iOS audio subsystem, which requires explicit session configuration
- Native macOS apps don't require AVAudioSession, but MacCatalyst inherits iOS behavior
- Without `AVAudioSession.SharedInstance().SetCategory(...)` and `SetActive(true)`, the audio hardware isn't initialized
- Querying `inputNode.GetBusOutputFormat(0)` on an uninitialized engine returns zeroed-out values

**Technical Evidence:**
- `GetBusOutputFormat(0)` returned: `sampleRate=0`, `channelCount=0`, `CommonFormat=Other`
- `InstallTapOnBus` failed with: `required condition is false: IsFormatSampleRateAndChannelCountValid(format)`
- The engine was created but the underlying hardware resources were never allocated

- **Fix:**
  1. **Configure AVAudioSession** before creating/using AVAudioEngine:
     - Call `AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.PlayAndRecord, ...)` to declare audio intent
     - Set preferred sample rate to 16000Hz for better compatibility with Gemini's expected input format
  2. **Activate AVAudioSession** via `SetActive(true)` to initialize audio hardware
  3. **Prepare the engine** with `_engine.Prepare()` to ensure hardware resources are allocated before accessing input node
  4. **Validate input format** before installing tap to provide clear error if hardware is still invalid
  5. **Add session interruption handling** to gracefully handle system audio interruptions (phone calls, Siri, etc.)

- **Files Changed:**
  - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/AudioService.cs` — Added AVAudioSession configuration, activation, engine preparation, and interruption handling

- **Tested:** ✅ Verified on macOS (December 14, 2025)
  - AVAudioSession category set to PlayAndRecord ✅
  - AVAudioSession activated with HW sample rate 48000Hz ✅
  - AVAudioEngine created and InputNode accessed ✅
  - Input tap installed (PCMFloat32/48000Hz/ch=1) ✅
  - AVAudioEngine started ✅
  - Captured frames continuously with RMS values (frame #1-#200+ verified) ✅
  - Manual float32→int16 conversion with 3:1 decimation working ✅

#### Change Log / Decisions (BUG-005)
- **Dec 14, 2025 — Observability improvements (why: eliminate "are we in mock?" + confirm mic pipeline stage)**
  - Added startup DI logs to stdout to confirm `IGeminiService` selection (Mock vs Live).
    - `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs`
  - Added footer indicator bound to ViewModel (`GeminiBackendText`) to display "Mock Gemini" vs "Gemini Live API".
    - `src/GaimerDesktop/GaimerDesktop/MainPage.xaml`
    - `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs`
  - Added MacCatalyst mic permission + recording start logs (`StartRecordingAsync`, auth status).
    - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/AudioService.cs`
  - Adjusted connect flow to avoid starting mic unless `IGeminiService.IsConnected` is true (why: don't start mic when connect fails).
    - `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs`
  - Added stable log capture file launcher (`run_maccatalyst.log`) (why: Cursor terminal logs were being cleared).
    - Launch procedure change (no repo file)
- **Dec 14, 2025 — Threading adjustment (why: AVFoundation sometimes misbehaves off UI-thread continuations)**
  - Removed `ConfigureAwait(false)` from MacCatalyst mic permission await to keep continuation on caller context.
    - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/AudioService.cs`
  - Wrapped `InstallTapOnBus` in try/catch and added "tap installed / engine started" logs.
    - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/AudioService.cs`

- **Dec 14, 2025 — Concrete root cause from logs (why: stop guessing)**
  - Observed `InstallTapOnBus` failure: **`com.apple.coreaudio.avfaudio` / `Input HW format is invalid`** when forcing tap format `PCMInt16/16k/mono`.
  - Decision: tap the input node's **native format**, then convert/resample to `PCM16/16k/mono` before sending to Gemini.
    - Implemented in `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/AudioService.cs`:
      - Tap format switched to `inputNode.GetBusOutputFormat(0)` (native)
      - Added `AVAudioConverter` path to convert to `PCMInt16/16000Hz/mono`

- **Dec 14, 2025 — New observation after native-format tap (why: next decision)**
  - `GetBusOutputFormat(0)` returned an **invalid format** in this environment (`sampleRate=0`, `channelCount=0`, `CommonFormat=Other`).
  - `InstallTapOnBus` fails with: **`required condition is false: IsFormatSampleRateAndChannelCountValid(format)`**
  - Implication: mic never becomes "hot", so OS mic indicator won't show and no audio frames can be produced until we establish a valid input node format (likely via correct node format API / engine prep/start / session activation).

- **Dec 14, 2025 — Latest finding (why: pinpoint stall)**
  - After adding AVAudioSession activation, logs show we reach:
    - `AVAudioSession activated...`
    - `AVAudioSession activation complete (continuing)`
    - then **no further progress** during mic start.
  - This indicates the flow is stalling during **AVAudioEngine initialization** (inside `EnsureEngineInitialized_NoLock()`), before the tap install path is reached.
  - Decision: add granular “before/after” logs + try/catch around `new AVAudioEngine()` and `_engine.Prepare()` to identify whether construction throws or `Prepare()` blocks.
    - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/AudioService.cs`

- **Dec 14, 2025 — Result of granular engine init logging**
  - `AVAudioEngine` construction succeeds.
  - `AVAudioEngine.Prepare()` throws `com.apple.coreaudio.avfaudio`:
    - `required condition is false: inputNode != nullptr || outputNode != nullptr`
  - Interpretation: engine graph initialization is failing at prepare-time; we need to avoid/replace `Prepare()` and/or force creation of input/output nodes before preparing.

- **Dec 14, 2025 — Root cause confirmed via research + fix implemented**
  - **Finding:** MacCatalyst uses the iOS audio stack, which requires AVAudioSession configuration and activation BEFORE the AVAudioEngine's input node will report a valid format.
  - **Cause:** The code created AVAudioEngine but never configured/activated AVAudioSession, so hardware resources were never allocated.
  - **Fix:** Added `AVAudioSession.SetCategory(PlayAndRecord)` + `SetActive(true)` before accessing input node format.
  - **Additional:** Added audio interruption notification observer for graceful handling of system interruptions.

- **Dec 14, 2025 — Iterative fixes during on-device testing**
  - **Issue 1:** `_engine.Prepare()` throws `inputNode != nullptr || outputNode != nullptr`
    - **Cause:** Cannot call Prepare() before accessing nodes - they don't exist yet
    - **Fix:** Removed Prepare() call; instead access InputNode to force its creation
  - **Issue 2:** AVAudioConverter crashes with `outputBuffer.frameCapacity >= inputBuffer.frameLength`
    - **Cause:** Output buffer allocated with decimated frame count, but converter needs >= input frames
    - **Fix:** Changed to allocate output buffer with input.FrameLength capacity
  - **Issue 3:** AVAudioConverter returns OSStatus error -50 (invalid parameter)
    - **Cause:** AVAudioConverter doesn't handle non-interleaved PCMFloat32 to interleaved PCMInt16 well
    - **Fix:** Implemented manual float32→int16 conversion with 3:1 decimation (48kHz→16kHz)
  
- **Dec 14, 2025 — VERIFIED WORKING**
  - Mic capture now works on MacCatalyst
  - Logs show continuous frame capture: `Captured frame #1: 3200 bytes, rms=0.001` through frame #200+
  - RMS values show actual signal (ambient noise ~0.002-0.003)

#### Commits (BUG-005)
- **Non-audio (UI/nav/DI indicators):** `db67387`
- **Audio-only (MacCatalyst AudioService):** `6fee3e7` (fix) / `8cb95be` (wip)

#### External Research Notes (BUG-005)
- **macOS privacy/TCC behavior (prompting is one-time):**
  - If mic permission is already **Authorized**, macOS will not prompt again (so "no prompt" is expected).
  - If a prompt never appears and the app doesn't show under Microphone privacy settings, the app likely never triggered a real mic access request.
  - Reference: Apple Support — [Control access to the microphone on Mac](https://support.apple.com/en-lamr/102071)
- **Resetting microphone permissions during debugging:**
  - `tccutil reset Microphone <bundle-id>` can reset mic permission so the prompt can be re-triggered on next access attempt.
  - Reference: SuperUser discussion — [Apps don't show up in camera and microphone privacy settings in MacBook](https://superuser.com/questions/1441270/apps-dont-show-up-in-camera-and-microphone-privacy-settings-in-macbook)
- **Operational implication for this bug:**
  - "No prompt" does **not** imply mic is inactive (it may already be authorized). The real proof is whether AVAudioEngine delivers input buffers (our capture-frame logs).
- **AVAudioSession requirement on MacCatalyst:**
  - MacCatalyst apps inherit iOS audio behavior requiring explicit session management
  - `AVAudioSession.SharedInstance().SetCategory()` declares the app's audio intent
  - `AVAudioSession.SharedInstance().SetActive(true)` activates the session and initializes hardware
  - Without activation, `inputNode.GetBusOutputFormat(0)` returns invalid/zeroed format
  - Reference: Apple Developer Documentation — AVAudioSession, AVAudioEngine

---

### BUG-006: Gemini Live API Connection Rejected / Model Not Found
- **ID:** BUG-006
- **Date Reported:** December 14, 2025
- **Date Fixed:** December 14, 2025
- **Severity:** High
- **Status:** ✅ Fixed (model name corrected; quota issue is external)
- **Component:** Network / Gemini Live API Integration
- **Description:** After establishing WebSocket connection to Gemini Live API, the server immediately closes the connection with an error. No AI audio responses are received.
- **Steps to Reproduce:**
  1. Launch app with valid `GEMINI_API_KEY` and `USE_MOCK_SERVICES=false`
  2. Select agent + game
  3. Click CONNECT
  4. Observe WebSocket connects but server closes connection immediately
- **Expected Behavior:** Gemini server should accept the setup message, return `setupComplete`, and maintain the connection for audio streaming.
- **Actual Behavior:** Server closes connection with `PolicyViolation` or `InternalServerError` status.

#### Root Cause Analysis (BUG-006)

**Issue 1: Model Not Found**
- **Error:** `CloseStatus: PolicyViolation, CloseDescription: models/gemini-2.5-flash-preview-native-audio-dialog is not found for API version v1beta, or is not supported for bidiGenerateContent`
- **Cause:** The original model name `gemini-2.5-flash-preview-native-audio-dialog` does not exist in the Gemini API
- **Fix:** Changed model to `gemini-2.0-flash-exp` which supports the Multimodal Live API

**Issue 2: Quota Exceeded (External)**
- **Error:** `CloseStatus: InternalServerError, CloseDescription: You exceeded your current quota, please check your plan and billing details`
- **Cause:** API key has hit its usage limit (free tier or billing issue)
- **Status:** Not a code bug - requires user to check their Google AI Studio account

- **Fix Applied:**
  - Updated `MODEL` constant from `models/gemini-2.5-flash-preview-native-audio-dialog` to `models/gemini-2.0-flash-exp`
  - Added comprehensive logging to GeminiLiveService for connection diagnostics:
    - Connection state changes
    - Setup message content
    - WebSocket close status and description
    - Audio send/receive counts
    - Receive loop status

- **Files Changed:**
  - `src/GaimerDesktop/GaimerDesktop/Services/GeminiLiveService.cs`

- **Tested:** ✅ Verified on macOS (December 14, 2025)
  - WebSocket connects successfully
  - Setup message accepted (no "model not found" error)
  - Quota error is external account issue, not code bug

#### Change Log (BUG-006)

- **Dec 14, 2025 — Added Gemini connection logging**
  - Added `Console.WriteLine` logs for: connecting, WebSocket connected, setup sent, connection established, receive loop started/ended
  - Added WebSocket close status and description logging
  - Added audio send counter (logs first 5 chunks + every 100th)
  - Added message receive counter (logs first 3 + every 10th)
  - Added setup message JSON logging for debugging

- **Dec 14, 2025 — Model name fix**
  - Changed from `gemini-2.5-flash-preview-native-audio-dialog` (invalid) to `gemini-2.0-flash-exp` (valid)
  - Verified: connection no longer rejected with "model not found"

#### Notes for Users
- If you see quota exceeded errors, check your [Google AI Studio](https://aistudio.google.com/) account
- Free tier has daily limits; consider upgrading for production use
- The app will still capture mic audio even if Gemini connection fails (useful for debugging)

---

### BUG-007: MacCatalyst Audio Playback Crashes When Recording Active
- **ID:** BUG-007
- **Date Reported:** December 15, 2025
- **Date Fixed:** (In Progress)
- **Severity:** High
- **Status:** 🔧 In Progress
- **Component:** Audio System / MacCatalyst (AVAudioEngine playback)
- **Description:** When OpenAI (or Gemini) sends audio response data, attempting to play it back crashes with an AVAudioEngine format error. The crash occurs in `EnsurePlayerInitialized_NoLock()` when trying to connect the player node to the mixer while recording is already active.
- **Steps to Reproduce:**
  1. Launch app with `VOICE_PROVIDER=openai` and valid `OPENAI_APIKEY`
  2. Select agent + game
  3. Click CONNECT
  4. Speak to trigger VAD detection
  5. Wait for OpenAI to respond with audio
  6. Observe crash when playback attempts to start
- **Expected Behavior:** Audio from OpenAI should play back through speakers while microphone continues recording.
- **Actual Behavior:** App crashes with `kAudioUnitErr_FormatNotSupported (-10868)` when trying to connect player node.

#### Error Details (BUG-007)

```
ObjCRuntime.ObjCException: Objective-C exception thrown.
Name: com.apple.coreaudio.avfaudio 
Reason: [[busArray objectAtIndexedSubscript:(NSUInteger)element] setFormat:format error:&nsErr]: returned false, 
error Error Domain=NSOSStatusErrorDomain Code=-10868 "(null)"

at AVFoundation.AVAudioEngine.Connect(AVAudioNode sourceNode, AVAudioNode targetNode, AVAudioFormat format)
at AudioService.EnsurePlayerInitialized_NoLock() in AudioService.cs:line 562
at AudioService.PlayAudioAsync(Byte[] pcmData) in AudioService.cs:line 290
```

#### Analysis (BUG-007)

- **Error Code -10868:** `kAudioUnitErr_FormatNotSupported` - the audio format is incompatible
- **Trigger:** `_engine.Connect(_playerNode, _engine.MainMixerNode, playbackFormat)` fails
- **Context:** AVAudioEngine is already running for recording when playback initialization is attempted
- **Hypothesis:** The playback format (24kHz PCM) conflicts with the recording format (48kHz native) when both are connected to the same engine's mixer node
- **Related:** BUG-005 documents the recording setup (48kHz native → 16kHz converted)

#### Fix Applied (December 15, 2025)

- **Root Cause Confirmed:** AVAudioEngine cannot reconfigure node connections while already running
- **Solution (Two-Part):**
  1. **Pre-initialize player node:** Connect `_playerNode` to `MainMixerNode` in `EnsureEngineInitialized_NoLock()` *before* engine starts
  2. **Format conversion at playback:** Use `AVAudioConverter` to convert 24kHz PCM16 → MainMixerNode's native format (44.1kHz Float32)
- **Files Modified:**
  - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/AudioService.cs`
- **Status:** ✅ Fixed - Playback now works without crash
- **Residual Issue:** Playback is ~1.8x slower due to sample rate conversion (see BUG-008)

---

### BUG-008: Audio Playback Slow on MacCatalyst (OpenAI)
- **ID:** BUG-008
- **Date Reported:** December 15, 2025
- **Date Fixed:** December 15, 2025
- **Severity:** Medium
- **Status:** ✅ Fixed
- **Component:** Audio System / MacCatalyst (AVAudioEngine playback)
- **Description:** When using the OpenAI provider, audio playback is noticeably slower than expected, causing the AI's speech to sound drawn-out.
- **Root Cause:** Sample rate conversion from 24kHz (OpenAI format) to 44.1kHz (MainMixerNode native format) uses a fractional ratio (1.8375x) which introduces timing issues.
- **Impact:**
  - AI speech sounds slow/drawn-out
  - Extended playback time allows microphone to capture AI voice (echo)
  - Echo suppression (`IsPlaying` check) holds audio too long
- **Fix Applied (commit `1a075d8`):**
  - Implemented separate `PlaybackService` with dedicated `AVAudioEngine`
  - Manual linear interpolation resampling: 24kHz PCM16 → 44.1kHz Float32 stereo
  - Connects player node with mixer's native format (no format conflicts)
  - Resampling is mathematically correct, ensuring proper playback speed
- **Files Modified:**
  - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/PlaybackService.cs`
- **Tested:** ✅ Verified smooth, correct-speed playback with OpenAI

---

### BUG-009: Echo/Feedback Loop with OpenAI VAD
- **ID:** BUG-009
- **Date Reported:** December 15, 2025
- **Date Fixed:** December 15, 2025
- **Severity:** Medium
- **Status:** ✅ Fixed
- **Component:** Audio System / Voice Activity Detection
- **Description:** OpenAI's server-side VAD detects the AI's own voice playing through speakers as user speech, causing a feedback loop.
- **Steps to Reproduce:**
  1. Connect with OpenAI provider
  2. Speak to trigger AI response
  3. AI responds, playing audio through speakers
  4. Microphone captures AI's voice
  5. OpenAI VAD detects "user speech" (actually AI's voice)
  6. AI responds to its own words
- **Root Cause:** Microphone continues capturing while AI is speaking. Slow playback (BUG-008) extends the window for echo capture.
- **Fix Applied (Two-Part):**
  1. **Echo suppression in ViewModel:**
    ```csharp
    if (_conversationProvider.IsConnected && !_audioService.IsPlaying)
    {
        _ = _conversationProvider.SendAudioAsync(pcm);
    }
    ```
  2. **Correct playback speed (BUG-008 fix):** Proper resampling in `PlaybackService` ensures `IsPlaying` duration matches actual speech duration
- **Result:** ✅ Echo eliminated - AI no longer responds to its own voice
- **Files Modified:**
  - `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs`
  - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/PlaybackService.cs`

---

## Bug Statistics

| Severity | Open | In Progress | Fixed | Won't Fix | Total |
|----------|------|-------------|-------|-----------|-------|
| Critical | 0 | 0 | 0 | 0 | 0 |
| High | 0 | 0 | 4 | 0 | 4 |
| Medium | 0 | 0 | 5 | 0 | 5 |
| Low | 0 | 0 | 0 | 0 | 0 |
| **Total** | **0** | **0** | **9** | **0** | **9** |

---

## Known Issues

### Current Known Issues

*(None - All reported bugs are fixed)*

### Resolved Issues

1. **BUG-002:** Connection drops when returning from MinimalView to MainView - **FIXED Dec 12, 2024**
   - Solution: Made MainViewModel a Singleton for shared state between views
2. **BUG-001:** Audio bars not animating - **FIXED Dec 13, 2025**
   - Solution: Bind visualizer to live volume + marshal updates to UI thread
3. **BUG-005:** MacCatalyst mic capture not producing audio frames - **FIXED Dec 14, 2025**
   - Solution: Configure and activate AVAudioSession before accessing AVAudioEngine input node
4. **BUG-006:** Gemini Live API connection rejected - **FIXED Dec 14, 2025**
   - Solution: Changed model from invalid `gemini-2.5-flash-preview-native-audio-dialog` to valid `gemini-2.0-flash-exp`
5. **BUG-007:** MacCatalyst audio playback crash with OpenAI - **FIXED Dec 15, 2025**
   - Solution: Pre-initialize player node before engine starts + use AVAudioConverter for format matching
6. **BUG-008:** Audio playback slow on MacCatalyst - **FIXED Dec 15, 2025**
   - Solution: Separate PlaybackService with manual linear interpolation resampling (24kHz → 44.1kHz)
7. **BUG-009:** Echo/feedback loop with OpenAI VAD - **FIXED Dec 15, 2025**
   - Solution: Echo suppression (`!_audioService.IsPlaying` check) + correct playback speed from BUG-008 fix

---

## Bug Categories

### Connection Issues
- WebSocket connection failures
- Reconnection problems
- API timeout errors
- Network interruption handling

### Audio Issues
- **BUG-001:** Volume level display not updating
- Microphone not detected
- Audio playback failures
- Volume level calculation errors
- Audio format conversion problems
- Device permission issues

### Window Capture Issues
- Window enumeration failures
- Capture quality problems
- Window closed detection failures
- Minimized window handling
- Thumbnail generation errors

### UI/UX Issues
- Layout problems
- Styling inconsistencies
- Performance issues
- Responsiveness problems

### Navigation Issues
- ~~**BUG-002:** State loss on navigation between views~~ (FIXED)

### Platform-Specific Issues
- Windows-specific bugs
- macOS-specific bugs
- Cross-platform compatibility issues

### Build & Distribution Issues
- Build failures
- Packaging problems
- Code signing issues (resolved with `-p:EnableCodeSigning=false` for dev)
- Installation problems

---

## Bug Reporting Guidelines

When reporting a bug, please include:
1. **Clear title** describing the issue
2. **Severity level** (Critical/High/Medium/Low)
3. **Component** affected
4. **Detailed description** of the problem
5. **Steps to reproduce** (if applicable)
6. **Expected vs actual behavior**
7. **Environment details** (OS version, .NET version, etc.)
8. **Screenshots/logs** (if applicable)
9. **Workaround** (if any)

---

## Severity Definitions

- **Critical:** Application crashes, data loss, security vulnerability, blocks core functionality
- **High:** Major feature broken, significant performance degradation, workaround available but inconvenient
- **Medium:** Feature partially broken, minor performance issues, cosmetic problems affecting usability
- **Low:** Minor cosmetic issues, edge cases, nice-to-have improvements

---

**Notes:**
- This log is maintained throughout the development lifecycle
- Bugs are tracked from discovery through resolution
- Statistics are updated regularly
- Known issues are documented for transparency

