# App Flow Charts

**Project:** Gaimer / Witness Desktop  
**Branch context:** `post-mini-dev`  
**Companion doc:** [APP_FLOW_DESIGN.md](/Users/tonynlemadim/Documents/5DOF%20Projects/gaimerV2/WITNESS/gaimer_spec_docs/APP_FLOW_DESIGN.md)

This file is the quick visual version of the current app flow.

## 1. End-to-End User Flow

```mermaid
flowchart TD
    A[Launch app] --> B[MauiProgram builds DI/config]
    B --> C[App.CreateWindow]
    C --> D[AppShell]
    D --> E[Onboarding]
    E --> F[Sign in]
    F --> G[Fetch and inject API keys]
    G --> H[Browse/select agent]
    H --> I[Optional download/setup]
    I --> J[Connect from onboarding]
    J --> K[MainPage]
    K --> L[Pick capture target]
    L --> M[Connect live session]
    M --> N[Main runtime loop]
    N --> O[Timeline updates]
    N --> P[Ghost mode updates]
    N --> Q[MinimalView optional]
    K --> R[Settings]
```

## 2. Boot and Composition Flow

```mermaid
flowchart TD
    A[MauiProgram.CreateMauiApp] --> B[Load .env and user secrets]
    B --> C[Register services]
    C --> D[Register viewmodels]
    D --> E[Register views]
    E --> F[Build MAUI app]
    F --> G[App]
    G --> H[CreateWindow]
    H --> I[Mark structural settings applied]
    I --> J[Window AppShell]
    J --> K[Onboarding route]
```

## 3. Provider Selection Flow

```mermaid
flowchart TD
    A[Startup config/settings] --> B{USE_MOCK_SERVICES?}
    B -- Yes --> C[Mock provider path]
    B -- No --> D{Voice provider selection}
    D --> E[ConversationProviderFactory]
    D --> F[BrainServiceFactory]

    E --> E1{InferenceMode LocalOnly?}
    E1 -- Yes --> E2[Local conversation provider]
    E1 -- No --> E3{Explicit VOICE_PROVIDER?}
    E3 -- Yes --> E4[Gemini / OpenAI / Local / Mock]
    E3 -- No --> E5{Persisted settings provider valid?}
    E5 -- Yes --> E6[Selected cloud voice provider]
    E5 -- No --> E7[Auto-detect Gemini then OpenAI]
    E7 --> E8[Mock if no keys]

    F --> F1[InferenceProviderPolicy]
    F1 --> F2{CloudOnly / LocalOnly / LocalFirst}
    F2 --> F3[OpenRouter brain]
    F2 --> F4[Local MiniCPM brain]
    F2 --> F5[Mock brain fallback]
```

## 4. Onboarding State Flow

```mermaid
stateDiagram-v2
    [*] --> SignIn
    SignIn --> AgentBrowse: sign-in success
    SignIn --> SignIn: sign-in failure
    AgentBrowse --> Downloading: download/setup
    Downloading --> AgentBrowse: download failure
    Downloading --> Ready: download success
    Ready --> MainPage: connect
```

## 5. Main Session Flow

```mermaid
flowchart TD
    A[MainPage loaded with agent] --> B[Select capture target]
    B --> C[Start capture on target]
    C --> D[ToggleConnectionAsync]
    D --> E[ConversationProvider.ConnectAsync]
    E --> F{Connected?}
    F -- No --> G[Stay disconnected]
    F -- Yes --> H[SessionManager -> InGame]
    H --> I[Live session active]
    I --> J[Voice toggle optional]
    I --> K[Send text optional]
    I --> L[Ghost mode optional]
    I --> M[MinimalView optional]
    I --> N[Disconnect]
    N --> O[StopSessionAsync]
    O --> P[OutGame cleanup]
```

## 6. Capture to Brain Flow

```mermaid
flowchart TD
    A[FrameCaptured] --> B[Append ReelMoment]
    B --> C[Scale preview image]
    C --> D[BrainEventRouter.OnScreenCapture]
    D --> E[Compress frame]
    E --> F[Frame diff gate]
    F -->|Changed| G[IBrainService.TrySubmitFrame]
    F -->|Unchanged| H[Skip frame]
    G --> I[Brain processing channel]
```

## 7. Brain and Router Flow

```mermaid
flowchart TD
    A[Brain service] --> B[BrainResult channel]
    B --> C[BrainEventRouter.StartConsuming]
    C --> D{BrainResult type}
    D --> E[ImageAnalysis]
    D --> F[ProactiveAlert]
    D --> G[ToolResult]
    D --> H[Error]

    E --> I[Timeline image analysis event]
    E --> J[Context ingest]
    E --> K[Journal entry / new game detection]

    F --> L[Timeline proactive event]
    F --> M[Optional voice contextual update]

    G --> N[Tool-call timeline events]
    G --> O[General chat reply]
    G --> P[Ghost tool-use card path]

    H --> Q[System error timeline event]
```

## 8. Chat Flow

```mermaid
flowchart TD
    A[User sends text] --> B{Conversation connected?}
    B -- No --> C[Out-game chat path]
    C --> D[BrainService.ChatAsync]
    D --> E[Assistant reply]
    E --> F[Timeline direct message]

    B -- Yes --> G[In-game chat path]
    G --> H[Route user message to timeline]
    H --> I[Build context envelope]
    I --> J[BrainService.SubmitQueryAsync]
    J --> K[Brain result channel]
    K --> L[BrainEventRouter]
    L --> M[Assistant reply callback]
```

## 9. Timeline Presentation Flow

```mermaid
flowchart TD
    A[TimelineFeed] --> B[Checkpoints newest first]
    B --> C[EventLines newest first]
    C --> D[Latest event marked expanded]
    D --> E[Previous latest collapses]
    E --> F[Older messages remain compressed/expandable]
    B --> G[Archived boundary checkpoint at end]
```

## 10. Ghost Mode Flow

```mermaid
flowchart TD
    A[FAB tapped] --> B{Ghost mode supported?}
    B -- No --> C[Toggle MAUI overlay only]
    B -- Yes --> D{Ghost mode active?}
    D -- No --> E[Set IsFabActive true]
    E --> F[EnterGhostModeAsync]
    F --> G[Set native panel state]
    G --> H[Ghost card updates from live events]

    D -- Yes --> I[Set IsFabActive false]
    I --> J[ExitGhostModeAsync]

    H --> K[Text event card]
    H --> L[Voice activity card]
    H --> M[Tool-use card with icon-led layout]
    H --> N[Alert/error card]
```

## 11. Navigation Flow

```mermaid
flowchart TD
    A[Onboarding] -->|GoToAsync MainPage| B[MainPage]
    B -->|GoToAsync Settings| C[SettingsPage]
    C -->|GoToAsync ..| B
    B -->|GoToAsync MinimalView| D[MinimalViewPage]
    D -->|GoToAsync MainPage| B
    B -->|ChangeAgent / GoToAsync ..| A
```

## 12. Current Verification Gate

```mermaid
flowchart TD
    A[Accepted implementation slices] --> B[Cloud live verification]
    B --> C{Finding type}
    C -- Provider/bootstrap/runtime --> D[Codex lane]
    C -- Timeline/settings/ghost presentation --> E[Claude lane]
    C -- No findings --> F[Ready for next product phase]
```

