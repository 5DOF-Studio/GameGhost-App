# Test Coverage Report — gAImer Desktop

## 1. Executive Summary

| Metric | Value |
|--------|-------|
| **Total tests** | **511 — ALL PASSING** |
| CI | GitHub Actions green on develop |
| Domains | Brain Pipeline, Chess Engine, ViewModels, Voice & Auth, Session & State, Timeline, Prompts, Personality, Frame Analysis, Audio, Models, Integration, UI Polish |
| Framework | xUnit 2.9 + Moq 4.20 + FluentAssertions 6.12 |
| Test project | `src/GaimerDesktop/GaimerDesktop.Tests/` |
| Build command | `dotnet build src/GaimerDesktop/GaimerDesktop.Tests/GaimerDesktop.Tests.csproj -f net8.0` |
| **Test execution** | **`dotnet test src/GaimerDesktop/GaimerDesktop.Tests/GaimerDesktop.Tests.csproj -f net8.0` (~11 seconds)** |

### Test Growth by Phase

| Phase | Tests Added | Running Total |
|-------|-------------|---------------|
| Phase 06 (initial infrastructure) | 140 | 140 |
| Phase 09 (Stockfish Chess Engine) | 127 | 267 |
| Phase 10 (Integration & Orchestration) | 157 | 424 |
| Phase 11 (Agent Personality) | 30 | 454 |
| Phase 08 (Bug Fix + UI Polish + CI) | 57 | 511 |

> **Note:** The detailed domain breakdown below covers the original 140-test infrastructure plan (Phase 06). Subsequent phases added tests documented in PROGRESS_LOG.md entries.

### Coverage Targets

| Tier | Target | Domains |
|------|--------|---------|
| Critical | 95%+ line coverage | Brain Pipeline, Context & Memory, Frame Analysis |
| Easy | 90%+ line coverage | Session & State, Timeline, Prompt Building |
| Trivial | 85%+ line coverage | Audio Utilities, Models & Serialization |

### Testing Standards

- No `Assert.True(true)` or dummy passing tests
- Every test exercises real logic with meaningful assertions
- Moq for interface dependencies, never concrete classes
- FluentAssertions for readable assertions
- Arrange-Act-Assert pattern consistently
- Test naming: `MethodName_Scenario_ExpectedBehavior`
- One assertion concept per test (multiple related assertions OK)

---

## 2. Test Inventory by Domain

### Domain A: Brain Pipeline (Critical — 30% of total coverage)

#### A1. ToolExecutor

**Class:** `ToolExecutor` (sealed)
**File:** `Services/Brain/ToolExecutor.cs`
**Dependencies:** `IWindowCaptureService`, `ISessionManager`, `OpenRouterClient`, `HttpClient` (Lichess), `string` (workerModel), `ILogger<ToolExecutor>`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| A1.1 | `ExecuteToolAsync_CaptureScreen_ReturnsBase64Image` | `ExecuteToolAsync("capture_screen", ...)` | Unit | P1 | Routes to capture handler, returns JSON with status/image_base64/target |
| A1.2 | `ExecuteToolAsync_GetGameState_ReturnsSessionInfo` | `ExecuteToolAsync("get_game_state", ...)` | Unit | P1 | Returns JSON with state, gameId, gameType, connectorName, tools list |
| A1.3 | `ExecuteToolAsync_GetBestMove_HappyPath` | `ExecuteToolAsync("get_best_move", ...)` | Unit | P1 | Parses FEN from args, calls Lichess, returns evaluation + bestMove + signal |
| A1.4 | `ExecuteToolAsync_WebSearch_HappyPath` | `ExecuteToolAsync("web_search", ...)` | Unit | P1 | Parses query, calls worker model via OpenRouterClient, returns answer |
| A1.5 | `ExecuteToolAsync_PlayerHistory_ReturnsNotAvailable` | `ExecuteToolAsync("player_history", ...)` | Unit | P2 | Returns Phase 07 "not_available" stub |
| A1.6 | `ExecuteToolAsync_PlayerAnalytics_ReturnsNotAvailable` | `ExecuteToolAsync("player_analytics", ...)` | Unit | P2 | Returns Phase 07 "not_available" stub |
| A1.7 | `ExecuteToolAsync_UnknownTool_ReturnsError` | `ExecuteToolAsync("invalid_tool", ...)` | Unit | P1 | Returns error JSON for unrecognized tool name |
| A1.8 | `ClassifyEvaluation_Over500_ReturnsOpportunity` | `ClassifyEvaluation(600)` | Unit | P1 | Signal="opportunity", assessment contains "decisive" |
| A1.9 | `ClassifyEvaluation_200To500_ReturnsOpportunity` | `ClassifyEvaluation(300)` | Unit | P1 | Signal="opportunity", assessment contains "significant" |
| A1.10 | `ClassifyEvaluation_100To200_ReturnsOpportunity` | `ClassifyEvaluation(150)` | Unit | P1 | Signal="opportunity", assessment contains "slight" |
| A1.11 | `ClassifyEvaluation_Neg100To100_ReturnsNeutral` | `ClassifyEvaluation(0)` | Unit | P1 | Signal="neutral", assessment contains "equal" or "even" |
| A1.12 | `ClassifyEvaluation_Neg200ToNeg100_ReturnsBlunder` | `ClassifyEvaluation(-150)` | Unit | P1 | Signal="blunder", assessment contains "slight" disadvantage |
| A1.13 | `ClassifyEvaluation_Neg500ToNeg200_ReturnsBlunder` | `ClassifyEvaluation(-300)` | Unit | P1 | Signal="blunder", assessment contains "significant" |
| A1.14 | `ClassifyEvaluation_BelowNeg500_ReturnsBlunder` | `ClassifyEvaluation(-600)` | Unit | P1 | Signal="blunder", assessment contains "decisive" |
| A1.15 | `GetBestMove_MissingFen_ReturnsNotAvailable` | `ExecuteToolAsync("get_best_move", "{}")` | Unit | P1 | Returns status="not_available" when FEN absent |
| A1.16 | `GetBestMove_LichessError_ReturnsGracefulFallback` | `ExecuteToolAsync("get_best_move", ...)` | Unit | P1 | Network failure returns error status, no exception thrown |
| A1.17 | `WebSearch_MissingQuery_ReturnsError` | `ExecuteToolAsync("web_search", "{}")` | Unit | P1 | Returns error when query field is missing |
| A1.18 | `WebSearch_LlmFailure_ReturnsError` | `ExecuteToolAsync("web_search", ...)` | Unit | P2 | OpenRouterClient exception caught, error JSON returned |
| A1.19 | `GetAvailableToolDefinitions_OutGame_Returns3Tools` | `GetAvailableToolDefinitions(session)` | Unit | P1 | web_search, player_history, player_analytics only |
| A1.20 | `GetAvailableToolDefinitions_InGame_Returns6Tools` | `GetAvailableToolDefinitions(session)` | Unit | P1 | All 6 tools including capture_screen, get_best_move, get_game_state |

#### A2. BrainEventRouter

**Class:** `BrainEventRouter` implements `IBrainEventRouter`
**File:** `Services/BrainEventRouter.cs`
**Dependencies:** `ITimelineFeed`, `IConversationProvider?`, `Action<string>?`, `IBrainContextService?`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| A2.1 | `RouteBrainResult_ImageAnalysis_CreatesTimelineEvent` | `RouteBrainResult(BrainResult)` | Unit | P1 | ImageAnalysis type creates EventOutputType.ImageAnalysis event |
| A2.2 | `RouteBrainResult_ProactiveAlert_MapSignalCorrectly` | `RouteBrainResult(BrainResult)` | Unit | P1 | ProactiveAlert type routes through signal mapping |
| A2.3 | `RouteBrainResult_ToolResult_CreatesTimelineEvent` | `RouteBrainResult(BrainResult)` | Unit | P1 | ToolResult creates timeline event |
| A2.4 | `RouteBrainResult_Error_CreatesTimelineEvent` | `RouteBrainResult(BrainResult)` | Unit | P1 | Error type handled gracefully, logged |
| A2.5 | `MapSignalToOutputType_Danger_ReturnsDanger` | `MapSignalToOutputType("danger")` | Unit | P1 | Maps to EventOutputType.Danger |
| A2.6 | `MapSignalToOutputType_Blunder_ReturnsDanger` | `MapSignalToOutputType("blunder")` | Unit | P1 | Maps to EventOutputType.Danger |
| A2.7 | `MapSignalToOutputType_Opportunity_ReturnsOpportunity` | `MapSignalToOutputType("opportunity")` | Unit | P1 | Maps to EventOutputType.Opportunity |
| A2.8 | `MapSignalToOutputType_Brilliant_ReturnsOpportunity` | `MapSignalToOutputType("brilliant")` | Unit | P1 | Maps to EventOutputType.Opportunity |
| A2.9 | `MapSignalToOutputType_Sage_ReturnsSageAdvice` | `MapSignalToOutputType("sage")` | Unit | P1 | Maps to EventOutputType.SageAdvice |
| A2.10 | `MapSignalToOutputType_Assessment_ReturnsAssessment` | `MapSignalToOutputType("assessment")` | Unit | P1 | Maps to EventOutputType.Assessment |
| A2.11 | `MapSignalToOutputType_Detection_ReturnsDetection` | `MapSignalToOutputType("detection")` | Unit | P1 | Maps to EventOutputType.Detection |
| A2.12 | `MapSignalToOutputType_Unknown_ReturnsSageAdvice` | `MapSignalToOutputType("xyz")` | Unit | P1 | Unknown signals default to SageAdvice |
| A2.13 | `OnProactiveAlert_HighUrgency_ForwardsToVoice` | `OnProactiveAlert(hint, commentary)` | Unit | P1 | When urgency=="high" and voice connected, sends to voice |
| A2.14 | `OnProactiveAlert_LowUrgency_SkipsVoice` | `OnProactiveAlert(hint, commentary)` | Unit | P1 | When urgency!="high", does NOT send to voice |
| A2.15 | `RouteBrainResult_IngestsL1Event` | `RouteBrainResult(BrainResult)` | Unit | P1 | Calls _brainContext.IngestEventAsync with correct BrainEventType |
| A2.16 | `L1Ingestion_ImageAnalysis_MapsToVisionObservation` | BrainResultType→BrainEventType | Unit | P1 | ImageAnalysis → VisionObservation |
| A2.17 | `L1Ingestion_ProactiveAlert_MapsToGameplayState` | BrainResultType→BrainEventType | Unit | P1 | ProactiveAlert → GameplayState |
| A2.18 | `L1Ingestion_Error_MapsToSystemSignal` | BrainResultType→BrainEventType | Unit | P1 | Error → SystemSignal |
| A2.19 | `VoiceForwarding_InterruptPriority_SendsImmediately` | Priority routing | Unit | P2 | Interrupt priority sends to voice |
| A2.20 | `VoiceForwarding_SilentPriority_SkipsVoice` | Priority routing | Unit | P2 | Silent priority does NOT send to voice |

#### A3. OpenRouterBrainService

**Class:** `OpenRouterBrainService` (sealed) implements `IBrainService`
**File:** `Services/Brain/OpenRouterBrainService.cs`
**Dependencies:** `OpenRouterClient`, `ToolExecutor`, `ISessionManager`, `string` (brainModel), `string` (workerModel)

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| A3.1 | `TruncateForVoice_ShortText_ReturnsUnchanged` | `TruncateForVoice("short", 200)` | Unit | P1 | Text under maxLen returned as-is |
| A3.2 | `TruncateForVoice_SentenceBoundary_TruncatesAtPeriod` | `TruncateForVoice(longText, 100)` | Unit | P1 | Truncates at last sentence boundary (., ?, !) before maxLen |
| A3.3 | `TruncateForVoice_NoSentenceBoundary_HardTruncate` | `TruncateForVoice(noPunctuation, 50)` | Unit | P1 | Falls back to hard truncate with "..." suffix |
| A3.4 | `TruncateForVoice_RetentionOver50Percent` | `TruncateForVoice(text, 100)` | Unit | P1 | Sentence boundary chosen only if >50% of maxLen retained |
| A3.5 | `CancelAll_ThreadSafe_SwapsCts` | `CancelAll()` | Unit | P1 | Interlocked.Exchange swaps CTS, old one cancelled |
| A3.6 | `IsBusy_NoActiveTasks_ReturnsFalse` | `IsBusy` | Unit | P2 | Returns false when _activeTasks == 0 |

---

### Domain B: Context & Memory (Critical — 15% of total coverage)

#### B1. BrainContextService

**Class:** `BrainContextService` (sealed) implements `IBrainContextService`
**File:** `Services/BrainContextService.cs`
**Dependencies:** `IVisualReelService`
**Constants:** `DefaultVoiceBudget=900`, `DefaultChatBudget=1200`, `HardMaxTokens=1600`, `CharsPerToken=4`, `MaxL1Events=200`, `L1Window=30s`, `L1RetentionWindow=5min`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| B1.1 | `GetContextForVoiceAsync_RespectsBudget` | `GetContextForVoiceAsync(...)` | Unit | P1 | Total tokens in envelope <= 900 |
| B1.2 | `GetContextForChatAsync_RespectsBudget` | `GetContextForChatAsync(...)` | Unit | P1 | Total tokens in envelope <= 1200 |
| B1.3 | `IngestEventAsync_AddsToL1Store` | `IngestEventAsync(evt)` | Unit | P1 | Event retrievable in subsequent GetContext call |
| B1.4 | `L1Events_AgePruning_RemovesOlderThan5Min` | `IngestEventAsync` + age | Unit | P1 | Events older than L1RetentionWindow pruned on ingest |
| B1.5 | `L1Events_CountCap_LimitsTo200` | `IngestEventAsync` x 210 | Unit | P1 | MaxL1Events enforced, oldest dropped |
| B1.6 | `L1Filtering_TimeWindow_Only30Seconds` | `BuildEnvelope` L1 window | Unit | P1 | Only events within L1Window (30s) included in L1 |
| B1.7 | `L1Filtering_ConfidenceThreshold_Drops_Below03` | `BuildEnvelope` confidence | Unit | P1 | Events with Confidence < 0.3 excluded |
| B1.8 | `L1Filtering_Staleness_RespectValidFor` | `BuildEnvelope` staleness | Unit | P1 | Events past ValidFor window excluded |
| B1.9 | `BuildL2Summary_GroupsByCategory` | `BuildL2Summary(events)` | Unit | P1 | Events grouped by category (threat, objective, etc.) |
| B1.10 | `BuildL2Summary_HighPriorityFirst` | `BuildL2Summary(events)` | Unit | P1 | threat/objective/critical/danger/warning categories first |
| B1.11 | `ComputeEnvelopeConfidence_NoEvents_Returns05` | `ComputeEnvelopeConfidence([], [])` | Unit | P1 | Base confidence 0.5 with no data |
| B1.12 | `ComputeEnvelopeConfidence_WithL1Events_BonusApplied` | `ComputeEnvelopeConfidence(l1, l2)` | Unit | P1 | L1 bonus of 0.1 added when L1 events present |
| B1.13 | `BudgetAllocation_Order` | `BuildEnvelope` | Unit | P1 | Allocation order: target → L1 → chat → L2 → reel |
| B1.14 | `BudgetExhaustion_GracefulTruncation` | `BuildEnvelope` with small budget | Unit | P1 | Each layer gracefully truncated, TruncationReport populated |
| B1.15 | `FormatAsPrefixedContextBlock_AllSections` | `FormatAsPrefixedContextBlock(env)` | Unit | P2 | Output contains all 6 context sections |

#### B2. VisualReelService

**Class:** `VisualReelService` (sealed) implements `IVisualReelService`
**File:** `Services/VisualReelService.cs`
**Properties:** `MaxCount { get; init; } = 500`, `MaxAgeSeconds { get; init; } = 300`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| B2.1 | `Append_IncreasesCount` | `Append(moment)` | Unit | P1 | Moment added, count increments |
| B2.2 | `GetRecent_NewestFirst` | `GetRecent(10)` | Unit | P1 | Returns moments in reverse chronological order |
| B2.3 | `GetRecent_RespectsCountLimit` | `GetRecent(5)` with 10 items | Unit | P1 | Returns at most requested count |
| B2.4 | `GetRecent_EmptyReel_ReturnsEmpty` | `GetRecent(10)` on empty | Unit | P2 | Returns empty list, no exception |
| B2.5 | `Trim_AgePolicy_RemovesOlderThanMaxAge` | `Append` with old moments | Unit | P1 | Moments older than MaxAgeSeconds removed |
| B2.6 | `Trim_CountPolicy_KeepsMaxCount` | `Append` x 510 | Unit | P1 | Oldest removed when count > MaxCount |
| B2.7 | `Trim_DualThreshold_AgeThenCount` | Combined age + count | Unit | P1 | Age pruning runs first, then count cap |

---

### Domain C: Session & State Management (Easy — 10% of total coverage)

#### C1. SessionManager

**Class:** `SessionManager` implements `ISessionManager`
**File:** `Services/SessionManager.cs`
**Events:** `EventHandler<SessionState>? StateChanged`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| C1.1 | `TransitionToInGame_SetsState` | `TransitionToInGame(id, type, name)` | Unit | P1 | State becomes InGame, GameId/GameType/ConnectorName set |
| C1.2 | `TransitionToOutGame_ClearsState` | `TransitionToOutGame()` | Unit | P1 | State becomes OutGame, all game fields null |
| C1.3 | `TransitionToInGame_SetsGameStartedAt` | `TransitionToInGame(...)` | Unit | P1 | GameStartedAt populated with ~UtcNow |
| C1.4 | `TransitionToOutGame_ClearsGameStartedAt` | `TransitionToOutGame()` | Unit | P1 | GameStartedAt becomes null |
| C1.5 | `StateChanged_FiresOnInGameTransition` | `TransitionToInGame(...)` | Unit | P1 | Event fires with SessionState.InGame |
| C1.6 | `StateChanged_FiresOnOutGameTransition` | `TransitionToOutGame()` | Unit | P1 | Event fires with SessionState.OutGame |
| C1.7 | `GetAvailableTools_OutGame_Returns3` | `GetAvailableTools()` | Unit | P1 | web_search, player_history, player_analytics |
| C1.8 | `GetAvailableTools_InGame_Returns6` | `GetAvailableTools()` | Unit | P1 | Above 3 + capture_screen, get_best_move, get_game_state |

#### C2. ToolDefinitions

**Class:** `ToolDefinitions` (static)
**File:** `Models/ToolDefinition.cs`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| C2.1 | `WebSearch_ParametersSchema_HasQueryField` | `ToolDefinitions.WebSearch.ParametersSchema` | Unit | P2 | JSON schema contains "query" property |
| C2.2 | `GetBestMove_ParametersSchema_HasFenField` | `ToolDefinitions.GetBestMove.ParametersSchema` | Unit | P2 | JSON schema contains "fen" property |
| C2.3 | `AllToolDefinitions_RequiresInGameFlags_Correct` | All static definitions | Unit | P2 | 3 tools RequiresInGame=false, 3 RequiresInGame=true |

---

### Domain D: Timeline (Easy — 10% of total coverage)

#### D1. TimelineFeed

**Class:** `TimelineFeed` implements `ITimelineFeed`
**File:** `Services/TimelineFeed.cs`
**Dependencies:** `ISessionManager`
**Events:** `EventHandler<TimelineCheckpoint>? CheckpointCreated`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| D1.1 | `NewCapture_CreatesInGameCheckpoint` | `NewCapture(ref, time, method)` | Unit | P1 | Checkpoint has Context=InGame, ScreenshotRef, GameTimeIn |
| D1.2 | `NewCapture_PrependsCheckpoint` | `NewCapture(...)` twice | Unit | P1 | Newest checkpoint at index 0 |
| D1.3 | `NewConversationCheckpoint_CreatesOutGameCheckpoint` | `NewConversationCheckpoint()` | Unit | P1 | Checkpoint has Context=OutGame |
| D1.4 | `AddEvent_CreatesEventLine` | `AddEvent(evt)` | Unit | P1 | New EventLine created for first event of a type |
| D1.5 | `AddEvent_GroupsSameOutputType` | `AddEvent(evt)` x 2 same type | Unit | P1 | Both events in same EventLine |
| D1.6 | `AddEvent_DifferentType_NewEventLine` | `AddEvent(evt)` x 2 diff type | Unit | P1 | Separate EventLines created |
| D1.7 | `EnsureCurrentCheckpoint_AutoCreates` | `AddEvent` with no checkpoint | Unit | P1 | Creates InGame or OutGame checkpoint automatically |
| D1.8 | `CheckpointCreated_EventFires` | `NewCapture(...)` | Unit | P2 | CheckpointCreated event raised |

#### D2. EventIconMap

**Class:** `EventIconMap` (static)
**File:** `Models/Timeline/EventIconMap.cs`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| D2.1 | `GetIcon_AllEventOutputTypes_ReturnNonNull` | `GetIcon(type)` for all values | Unit | P2 | Every EventOutputType has a non-null icon |
| D2.2 | `GetCapsuleColorHex_AllTypes_ReturnValidHex` | `GetCapsuleColorHex(type)` for all | Unit | P2 | Every type returns valid ARGB hex string |
| D2.3 | `GetCapsuleStrokeHex_AllTypes_ReturnValidHex` | `GetCapsuleStrokeHex(type)` for all | Unit | P2 | Every type returns valid ARGB hex string |

---

### Domain E: Prompt Building (Easy — 5% of total coverage)

#### E1. ChatPromptBuilder

**Class:** `ChatPromptBuilder` implements `IChatPromptBuilder`
**File:** `Services/ChatPromptBuilder.cs`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| E1.1 | `BuildSystemPrompt_ContainsCoreIdentity` | `BuildSystemPrompt(session, tools)` | Unit | P1 | Output contains "Dross" identity |
| E1.2 | `BuildSystemPrompt_ContainsBehaviorRules` | `BuildSystemPrompt(session, tools)` | Unit | P1 | Output contains "NEVER DO" section |
| E1.3 | `BuildSystemPrompt_InGame_ContainsInGameRules` | `BuildSystemPrompt(inGameSession, tools)` | Unit | P1 | Output contains in-game behavior rules |
| E1.4 | `BuildSystemPrompt_OutGame_ContainsOutGameRules` | `BuildSystemPrompt(outGameSession, tools)` | Unit | P1 | Output contains out-game behavior rules |
| E1.5 | `BuildSessionContextBlock_InGame_IncludesGameTime` | `BuildSessionContextBlock(inGameSession)` | Unit | P1 | Contains game type, elapsed time, connector |
| E1.6 | `BuildSessionContextBlock_OutGame_IncludesPlayerInfo` | `BuildSessionContextBlock(outGameSession)` | Unit | P1 | Contains player name, tier, tool availability |
| E1.7 | `BuildToolInstructions_ListsAvailableTools` | `BuildToolInstructions(tools)` | Unit | P2 | All tool names appear in output |

---

### Domain F: Frame Analysis (Critical — 10% of total coverage)

#### F1. FrameDiffService

**Class:** `FrameDiffService` implements `IFrameDiffService`
**File:** `Services/FrameDiffService.cs`
**Events:** `EventHandler<FrameChangeEventArgs>? FrameChanged`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| F1.1 | `ComputeHash_SameImage_SameHash` | `ComputeHash(imageData)` | Unit | P1 | Deterministic — identical input produces identical hash |
| F1.2 | `ComputeHash_DifferentImages_DifferentHashes` | `ComputeHash(img1)` vs `ComputeHash(img2)` | Unit | P1 | Distinct images produce distinct hashes |
| F1.3 | `CompareHashes_IdenticalHashes_ReturnsZero` | `CompareHashes(hash, hash)` | Unit | P1 | Hamming distance = 0 for identical hashes |
| F1.4 | `CompareHashes_KnownPair_ExpectedDistance` | `CompareHashes(h1, h2)` | Unit | P1 | Known hash pair produces expected Hamming distance via PopCount |
| F1.5 | `HasChanged_FirstCall_AlwaysTrue` | `HasChanged(imageData)` | Unit | P1 | First call returns true (no previous hash) |
| F1.6 | `HasChanged_SameImage_ReturnsFalse` | `HasChanged(img)` x 2 | Unit | P1 | Same image twice returns false |
| F1.7 | `HasChanged_Debounce_RespectsCooldown` | `HasChanged(img1)` then `HasChanged(img2)` within 1.5s | Unit | P1 | Returns false during debounce window |
| F1.8 | `HasChanged_ThresholdBoundary_AtThreshold` | `HasChanged(img, threshold=10)` | Unit | P1 | Hamming distance exactly at threshold returns false |
| F1.9 | `HasChanged_AboveThreshold_ReturnsTrue` | `HasChanged(img, threshold=10)` | Unit | P1 | Hamming distance above threshold returns true |
| F1.10 | `HasChanged_AboveThreshold_FiresFrameChangedEvent` | `HasChanged(img)` | Unit | P1 | FrameChanged event fires with correct FrameChangeEventArgs |

#### F2. ImageProcessor

**Class:** `ImageProcessor` (static)
**File:** `Services/ImageProcessor.cs`
**Constants:** `DefaultScale=0.5f`, `DefaultJpegQuality=60`, `ThumbnailSize=64`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| F2.1 | `ScaleAndCompress_HalvesResolution` | `ScaleAndCompress(pngData, 0.5f)` | Unit | P1 | Output width/height = input/2 |
| F2.2 | `ScaleAndCompress_OutputIsJpeg` | `ScaleAndCompress(pngData)` | Unit | P2 | Output starts with JPEG magic bytes (0xFF 0xD8) |
| F2.3 | `ScaleToHeight_MaintainsAspectRatio` | `ScaleToHeight(data, 100)` | Unit | P2 | Width/height ratio preserved |
| F2.4 | `GenerateThumbnail_CenterCropsToSquare` | `GenerateThumbnail(data, 64)` | Unit | P1 | Output is exactly 64x64 pixels |

---

### Domain G: Audio Utilities (Trivial — 5% of total coverage)

#### G1. AudioResampler

**Class:** `AudioResampler` (static)
**File:** `Services/Audio/AudioResampler.cs`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| G1.1 | `Resample_SameRate_ReturnsInput` | `Resample(data, 16000, 16000)` | Unit | P1 | Input returned unchanged when rates equal |
| G1.2 | `Resample_Upsample_CorrectSampleCount` | `Resample(data, 16000, 24000)` | Unit | P1 | Output sample count = input * (24000/16000) |
| G1.3 | `Resample_Downsample_CorrectSampleCount` | `Resample(data, 24000, 16000)` | Unit | P1 | Output sample count = input * (16000/24000) |
| G1.4 | `StereoToMono_AveragesChannels` | `StereoToMono(stereoData)` | Unit | P1 | Each output sample = (L + R) / 2 |
| G1.5 | `StereoToMono_HalvesByteCount` | `StereoToMono(stereoData)` | Unit | P2 | Output length = input length / 2 |
| G1.6 | `Float32ToInt16_RangesClamped` | `Float32ToInt16(floatData)` | Unit | P1 | Values clamped to Int16.MinValue..Int16.MaxValue |
| G1.7 | `Float32ToInt16_OutputLength` | `Float32ToInt16(floatData)` | Unit | P2 | Output length = input length / 2 (4 bytes → 2 bytes per sample) |
| G1.8 | `Resample_NullInput_Throws` | `Resample(null, ...)` | Unit | P2 | Throws ArgumentNullException |

#### G2. AudioFormat

**Class:** `AudioFormat` (static)
**File:** `Services/Audio/AudioFormat.cs`
**Constants:** `StandardInputSampleRate=16000`, `StandardOutputSampleRate=24000`, `BitsPerSample=16`, `Channels=1`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| G2.1 | `InputBytesForDuration_1Second_Returns32000` | `InputBytesForDuration(1000)` | Unit | P2 | 16000 * 2 * 1 = 32000 bytes/sec |
| G2.2 | `OutputBytesForDuration_1Second_Returns48000` | `OutputBytesForDuration(1000)` | Unit | P2 | 24000 * 2 * 1 = 48000 bytes/sec |
| G2.3 | `DurationCalculations_LinearScaling` | Multiple durations | Unit | P2 | 500ms = half of 1000ms output |

---

### Domain H: Models & Serialization (Trivial — 5% of total coverage)

#### H1. ContentConverter

**Class:** `ContentConverter : JsonConverter<object?>`
**File:** `Models/OpenRouterModels.cs`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| H1.1 | `Read_StringContent_ReturnsString` | `ContentConverter.Read(...)` | Unit | P1 | JSON string → C# string |
| H1.2 | `Read_ArrayContent_ReturnsListContentPart` | `ContentConverter.Read(...)` | Unit | P1 | JSON array → List<ContentPart> |
| H1.3 | `Read_NullContent_ReturnsNull` | `ContentConverter.Read(...)` | Unit | P2 | JSON null → null |
| H1.4 | `Write_String_WritesJsonString` | `ContentConverter.Write(...)` | Unit | P2 | C# string → JSON string |
| H1.5 | `Write_ListContentPart_WritesJsonArray` | `ContentConverter.Write(...)` | Unit | P2 | List<ContentPart> → JSON array |

#### H2. OpenRouterJsonContext

**Class:** `OpenRouterJsonContext` (source-generated)
**File:** `Models/OpenRouterModels.cs`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| H2.1 | `RoundTrip_OpenRouterRequest_PreservesData` | Serialize + Deserialize | Unit | P1 | All fields survive round-trip with snake_case naming |
| H2.2 | `RoundTrip_OpenRouterResponse_PreservesData` | Serialize + Deserialize | Unit | P1 | Response with choices, usage survives round-trip |
| H2.3 | `Serialization_NullsOmitted` | Serialize request with nulls | Unit | P2 | WhenWritingNull policy omits null fields |

#### H3. BrainResult

**Record:** `BrainResult`
**File:** `Models/BrainResult.cs`

| # | Test Name | Method Under Test | Type | Priority | What It Validates |
|---|-----------|-------------------|------|----------|-------------------|
| H3.1 | `BrainResult_DefaultPriority_WhenIdle` | `new BrainResult { ... }` | Unit | P2 | Default Priority is WhenIdle |
| H3.2 | `BrainResult_DefaultCreatedAt_UtcNow` | `new BrainResult { ... }` | Unit | P2 | CreatedAt defaults to ~UtcNow |

---

## 3. Integration Tests (10% of total — separate from unit)

These tests require external services or multi-component interaction:

| # | Test Name | Components | Type | What It Validates |
|---|-----------|-----------|------|-------------------|
| I1 | `LichessCloudEval_KnownPosition_ReturnsEval` | HttpClient → Lichess API | Integration | Real HTTP call with known FEN returns centipawn evaluation |
| I2 | `OpenRouterClient_ChatCompletion_ReturnsResponse` | OpenRouterClient → OpenRouter API | Integration | Real API call with simple prompt returns response (requires OPENROUTER_APIKEY) |
| I3 | `ChannelBrainResult_MultipleProducers_ConsumerReadsAll` | Channel<BrainResult> | Integration | Multiple concurrent writes all read by single consumer |
| I4 | `BrainEventRouter_ChannelConsumer_RoutesAllResults` | BrainEventRouter + Channel + TimelineFeed | Integration | Router reads from channel and adds events to timeline |
| I5 | `BrainContextService_FullEnvelope_EndToEnd` | BrainContextService + VisualReelService | Integration | Ingest events → build envelope → verify all layers populated |
| I6 | `SessionManager_TransitionCycle_ToolsReflectState` | SessionManager + ToolDefinitions | Integration | Full OutGame → InGame → OutGame cycle with tool list verification |

---

## 4. UI Tests (Not in scope — documented for future)

Platform-dependent tests requiring MAUI test host. Deferred to Phase 08 (Polish):

- **MainViewModel** — connect/disconnect lifecycle, command execution, binding updates
- **View code-behind** — timer logic, animation triggers, UI event handlers
- **Navigation flows** — agent selection → main page → minimal view transitions
- **XAML bindings** — compiled binding validation, converter correctness in UI context
- **Ghost Mode** — native panel show/hide, opacity, click-through (requires AppKit)

---

## 5. Coverage Summary Matrix

| Domain | Tests | Priority | % of Total | Estimated LOC |
|--------|-------|----------|------------|---------------|
| A: Brain Pipeline (A1+A2+A3) | 46 | Critical | 30% | ~800 |
| B: Context & Memory (B1+B2) | 22 | Critical | 15% | ~500 |
| C: Session & State (C1+C2) | 11 | Easy | 10% | ~250 |
| D: Timeline (D1+D2) | 11 | Easy | 10% | ~250 |
| E: Prompt Building (E1) | 7 | Easy | 5% | ~150 |
| F: Frame Analysis (F1+F2) | 14 | Critical | 10% | ~350 |
| G: Audio Utilities (G1+G2) | 11 | Trivial | 5% | ~250 |
| H: Models & Serialization (H1-H3) | 10 | Trivial | 5% | ~200 |
| Integration Tests (I1-I6) | 6 | Critical | 10% | ~300 |
| **Total** | **138** | | **100%** | **~3,050** |

---

## 6. Implementation Order

### Phase 1: Critical Path (82 tests) — ✅ COMPLETE (Mar 3, 2026)
**Brain Pipeline + Frame Analysis + Context & Memory — 84/84 test cases passing**

Wave 1 (no dependencies):
- A1: ToolExecutor (20 tests) ✅ — all static dispatch logic, ClassifyEvaluation
- F1: FrameDiffService (10 tests) ✅ — dHash, Hamming distance, debounce
- F2: ImageProcessor (4 tests) ✅ — scale, compress, thumbnail

Wave 2 (after A1):
- A2: BrainEventRouter (20 tests) ✅ — signal mapping, priority routing, L1 ingestion
- A3: OpenRouterBrainService (6 tests) ✅ — TruncateForVoice, CancelAll, IsBusy

Wave 3 (after A2):
- B1: BrainContextService (15 tests) ✅ — token budget, L1/L2 filtering, envelope building
- B2: VisualReelService (7 tests) ✅ — retention policies, ordering

### Phase 2: Easy Path (29 tests) — ✅ COMPLETE (Mar 3, 2026)
**Session + Timeline + Prompts — 113/113 total test cases passing**

- C1: SessionManager (8 tests) ✅ — state transitions, events, tool gating
- C2: ToolDefinitions (3 tests) ✅ — JSON Schema validation, RequiresInGame flags
- D1: TimelineFeed (8 tests) ✅ — checkpoint creation, event grouping, auto-checkpoint
- D2: EventIconMap (3 tests) ✅ — icon/color/stroke for all 11 generic types
- E1: ChatPromptBuilder (7 tests) ✅ — identity, rules, context blocks, tool listing

### Phase 3: Trivial Path (21 tests) — ✅ COMPLETE (Mar 3, 2026)
**Audio + Models — 140/140 total test cases passing**

- G1: AudioResampler (8 tests) ✅ — resample fast path, up/downsample, stereo-mono, float32-int16
- G2: AudioFormat (3 tests) ✅ — byte duration calculations, linear scaling
- H1: ContentConverter (5 tests) ✅ — polymorphic JSON read/write (string, array, null)
- H2: OpenRouterSerialization (3 tests) ✅ — round-trip snake_case, null omission
- H3: BrainResult (2 tests) ✅ — default Priority, default CreatedAt

### Phase 4: Integration (6 tests) — ✅ COMPLETE (Mar 3, 2026)
**Channel pipeline + end-to-end + live API**

- I1: LichessCloudEval (1 test) ✅ — real HTTP call, graceful skip on no network
- I2: OpenRouterClient (1 test) ✅ — real API call, skip if no OPENROUTER_APIKEY
- I3: ChannelBrainResult (1 test) ✅ — multi-producer concurrent writes, single consumer reads all
- I4: BrainEventRouter_Channel (1 test) ✅ — router reads from channel, routes to real TimelineFeed
- I5: BrainContextService (1 test) ✅ — ingest events → build envelope → format text block
- I6: SessionManager_Cycle (1 test) ✅ — full OutGame → InGame → OutGame with tool list

---

## 7. Test Project Details

### Project Location
```
src/GaimerDesktop/GaimerDesktop.Tests/
  GaimerDesktop.Tests.csproj    — xUnit + Moq + FluentAssertions (dual-target: net8.0 + net8.0-maccatalyst)
  GlobalUsings.cs                — Shared test imports
  Directory.Build.targets        — Mono target overrides for MacCatalyst library build
```

### Build & Run
```bash
# Run all tests (~4 seconds, no IDE required)
dotnet test src/GaimerDesktop/GaimerDesktop.Tests/GaimerDesktop.Tests.csproj -f net8.0

# Build only (no execution)
dotnet build src/GaimerDesktop/GaimerDesktop.Tests/GaimerDesktop.Tests.csproj -f net8.0

# Build for MacCatalyst (compile check only — cannot execute via CLI)
dotnet build src/GaimerDesktop/GaimerDesktop.Tests/GaimerDesktop.Tests.csproj \
  -f net8.0-maccatalyst -p:EnableCodeSigning=false
```

### How `dotnet test` Works (net8.0 Multi-Target Solution)
MacCatalyst uses Mono runtime; `dotnet test` CLI uses CoreCLR — fundamentally incompatible for test execution. Solution implemented (Mar 3, 2026):

1. **Both projects dual-target:** `net8.0` (library, no MAUI) alongside platform TFMs
2. **Conditional MAUI:** `<UseMaui>` and `<OutputType>Exe</OutputType>` only for platform targets
3. **TestStubs.cs:** Provides `ImageSource` and `MainThread` stubs for net8.0 (guarded by `#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS`)
4. **Compile exclusions:** Views, Controls, ViewModels, Utilities, platform code excluded from net8.0 build
5. **Result:** Services, Models, and interfaces compile for net8.0 — all unit tests run via `dotnet test -f net8.0`

### Shared Test Helpers
| Helper | Purpose |
|--------|---------|
| `Helpers/TestImageFactory.cs` | Generates gradient/checkerboard PNGs via SkiaSharp (dHash requires pixel variation — solid colors produce hash=0) |
| `Helpers/MockHttpHandler.cs` | Configurable `HttpMessageHandler` for mocking HttpClient at transport level (OpenRouterClient, Lichess API are concrete/sealed) |
| `Helpers/ReflectionHelper.cs` | Invokes private static methods for targeted testing (e.g., `TruncateForVoice`) |

### Folder Structure (actual — Phase 1 implemented)
```
GaimerDesktop.Tests/
  Helpers/
    TestImageFactory.cs          ✅ Implemented
    MockHttpHandler.cs           ✅ Implemented
    ReflectionHelper.cs          ✅ Implemented
  Brain/
    ToolExecutorTests.cs         ✅ 20 tests passing
    BrainEventRouterTests.cs     ✅ 20 tests passing
    OpenRouterBrainServiceTests.cs ✅ 6 tests passing
  Context/
    BrainContextServiceTests.cs  ✅ 15 tests passing
    VisualReelServiceTests.cs    ✅ 7 tests passing
  FrameAnalysis/
    FrameDiffServiceTests.cs     ✅ 10 tests passing
    ImageProcessorTests.cs       ✅ 4 tests passing
  Session/
    SessionManagerTests.cs       ✅ 8 tests passing
    ToolDefinitionTests.cs       ✅ 3 tests passing
  Timeline/
    TimelineFeedTests.cs         ✅ 8 tests passing
    EventIconMapTests.cs         ✅ 3 tests passing
  Prompts/
    ChatPromptBuilderTests.cs    ✅ 7 tests passing
  Audio/
    AudioResamplerTests.cs       ✅ 8 tests passing
    AudioFormatTests.cs          ✅ 3 tests passing
  Models/
    ContentConverterTests.cs     ✅ 5 tests passing
    OpenRouterSerializationTests.cs ✅ 3 tests passing
    BrainResultTests.cs          ✅ 2 tests passing
  Integration/
    ChannelPipelineTests.cs      ✅ 2 tests passing
    EndToEndTests.cs             ✅ 2 tests passing
    LiveApiTests.cs              ✅ 2 tests passing (env-var gated)
    EndToEndRoutingTests.cs
```
