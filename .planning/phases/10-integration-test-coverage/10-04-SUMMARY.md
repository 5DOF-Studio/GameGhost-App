# Phase 10 Plan 04: AgentSelection + Voice Guards + Integration Sweep Summary

AgentSelectionViewModel under net8.0 with Stockfish lifecycle tests, voice service guard paths verified without WebSocket, and real-component integration seams exercised end-to-end.

## Results

| Metric | Value |
| --- | --- |
| Tests added | 46 |
| Tests total | 458/458 passing |
| Duration | ~16 minutes |
| Commits | 4 task + 1 metadata |

## Tasks Completed

### Task 1: Enable AgentSelectionViewModel for net8.0
- Added csproj `<Compile Include>` for `AgentSelectionViewModel.cs`
- Extended TestStubs.cs with: Shell.GoToAsync dict overload, INavigation, ContentPage, MainPage
- Added Application.Handler/MauiContext stubs (service locator compiles but returns null)
- Added IDispatcher stub for CommunityToolkit.Mvvm source generators
- Added global using for `Microsoft.Extensions.DependencyInjection` (GetService<T> extension)
- **Commit:** `78e304f`

### Task 2: AgentSelectionViewModel Tests (19 tests)
- Constructor: parameterless init, with/null stockfish service, agent list contents
- SelectAgentAsync: null/empty/unknown ID guards, download overlay trigger, engine start/skip
- DownloadStockfishAsync: null service noop, success lifecycle with TaskCompletionSource, failure/exception error paths
- SkipDownloadAsync: hides overlay
- IsStockfishInstalled delegation, initial state verification
- **Commit:** `56603af`

### Task 3: Voice Service Guard Tests (21 tests)
- GeminiLiveService: null agent ArgumentNullException, empty API key Error+event, send audio/text/image when disconnected, dispose idempotent, initial state
- OpenAIRealtimeService: null agent, empty API key, send audio/text/commit/cancel when disconnected, dispose idempotent, initial state
- Provider properties: SupportsVideo (Gemini=true, OpenAI=false), ProviderName strings
- Provider error forwarding: empty API key errors propagate through adapter layer
- **Commit:** `7aea1d5`

### Task 4: Integration Seam Verification (6 tests)
- FrameDiff->Brain->Channel->Router: real FrameDiffService detects change, MockBrainService writes to channel, BrainEventRouter consumes and routes to timeline, BrainContextService ingests L1 event
- SessionManager tool consistency: OutGame 3 tools, InGame 7 tools, tools superset verified, round-trip back to OutGame
- BrainContext L1->voice: ingest vision+threat events, GetContextForVoiceAsync returns them, FormatAsPrefixedContextBlock produces text block
- MockConversationProvider lifecycle: factory creates, connect->Connected, disconnect->Disconnected
- Stockfish agent type consistency: chess agents have matching tools/capture/brain info, voice genders differ
- MockBrainService Channel round-trip: SubmitQueryAsync -> WaitToReadAsync -> TryRead -> verify result
- **Commit:** `041efad`

## Test Breakdown by File

| File | Tests | Category |
| --- | --- | --- |
| ViewModels/AgentSelectionViewModelTests.cs | 19 | ViewModel |
| Conversation/VoiceServiceGuardTests.cs | 21 | Service guards |
| Integration/PipelineIntegrationTests.cs | 6 | Integration seams |

## Phase 10 Complete Test Summary

| Plan | Tests Added | Running Total |
| --- | --- | --- |
| 10-01 Foundation | 31 | 332 |
| 10-02 Factory + Auth | 27 | 359 |
| 10-03 MainViewModel | 53 | 412 |
| 10-04 AgentSelection + Guards + Integration | 46 | 458 |
| **Total Phase 10** | **157** | **458** |

## Decisions Made

| Decision | Rationale |
| --- | --- |
| 19 AgentSelectionVM tests (vs planned 12) | Added initial state, gated agent exclusion, engine-already-ready, and exception path tests |
| 21 voice guard tests (vs planned 15) | Added initial state, send image/text disconnected, and provider error forwarding tests |
| 6 integration tests (vs planned 5) | Added MockBrainService channel round-trip as separate seam verification |
| Global using for DI extensions | Required for GetService<T>() to compile under net8.0 without MAUI |
| TestStubs: MainPage in GaimerDesktop namespace | Matches MainPage.xaml.cs namespace; enables AgentSelectionViewModel navigation compilation |

## Deviations from Plan

None -- plan executed as written with extra coverage tests added.

## Key Files

### Created
- `src/GaimerDesktop/GaimerDesktop.Tests/ViewModels/AgentSelectionViewModelTests.cs`
- `src/GaimerDesktop/GaimerDesktop.Tests/Conversation/VoiceServiceGuardTests.cs`
- `src/GaimerDesktop/GaimerDesktop.Tests/Integration/PipelineIntegrationTests.cs`

### Modified
- `src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj` (AgentSelectionViewModel re-include)
- `src/GaimerDesktop/GaimerDesktop/TestStubs.cs` (Shell, Navigation, ContentPage, MainPage, Handler, MauiContext, IDispatcher, DI global using)

## Coverage Audit

### Well-Covered Areas
- Models: Agent, BrainResult, ToolDefinition, VoiceConfig, ConnectionState
- Services: SessionManager, BrainContextService, FrameDiffService, TimelineFeed, BrainEventRouter
- Brain: MockBrainService, OpenRouterBrainService, OpenRouterClient, ToolExecutor
- Chess: StockfishService, FenValidator, UCI parsing
- ViewModels: MainViewModel (53 tests), AgentSelectionViewModel (19 tests)
- Conversation: ConversationProviderFactory, MockConversationProvider
- Auth: SupabaseAuthService, MockAuthService
- Voice: GeminiLiveService guards, OpenAIRealtimeService guards

### Known Gaps (Accepted)
- AgentSelectionViewModel VoiceGender path: Application.Current service locator returns null in tests
- Voice services WebSocket paths: ClientWebSocket is sealed/internal, can't mock
- Platform-specific code: Views, Controls, Platforms -- excluded from net8.0 by design
- OpenRouterBrainService live API paths: requires OPENROUTER_APIKEY
- GhostMode native interop: requires MacCatalyst runtime
