# Phase 09 Verification: Stockfish Chess Engine

## Status: PASSED

## Goal
Replace Lichess cloud eval with local Stockfish engine. On-demand download when user selects chess agent. Dual-tool architecture: engine analysis (authoritative) + LLM strategic analysis (explanatory). FEN validation, UCI wrapper, download UX with "Chess Skills" framing.

## Must-Haves Verification

### 1. IStockfishService abstraction
- [x] Interface with IsReady, IsInstalled, EnsureInstalledAsync, StartAsync, StopAsync, AnalyzePositionAsync
- [x] StockfishService: real Process management, UCI protocol, SemaphoreSlim serialization
- [x] MockStockfishService: deterministic canned results for 3 FEN positions
- [x] 53 unit tests covering FenValidator, UciParser, StockfishService, lifecycle

### 2. Dual chess tools registered
- [x] ToolDefinitions.AnalyzePositionEngine — FEN required, depth/num_lines optional
- [x] ToolDefinitions.AnalyzePositionStrategic — focus/player_color optional
- [x] SessionManager InGame tools: 7 total (3 always + 4 InGame including both chess tools)
- [x] Legacy get_best_move preserved for backward compatibility

### 3. ToolExecutor dispatches both tools
- [x] analyze_position_engine: FEN validation -> Stockfish (if ready) -> Lichess fallback -> formatted JSON
- [x] analyze_position_strategic: builds prompt -> OpenRouter LLM -> formatted JSON
- [x] GetAvailableToolDefinitions reads ParametersSchema directly (no hardcoded switch)

### 4. Agent system prompts updated
- [x] ChessToolGuidance constant with dual-tool guidance, FEN extraction rules, voice output conventions
- [x] Appended to both Leroy and Wasp SystemInstruction
- [x] Tools list updated: analyze_position_engine + analyze_position_strategic replace get_best_move
- [x] Derek (RPG) agent unchanged — no chess tools

### 5. Download UX
- [x] "Chess Skills" download overlay in AgentSelectionPage.xaml
- [x] DOWNLOAD button triggers EnsureInstalledAsync with progress bar
- [x] SKIP FOR NOW navigates without Stockfish (Lichess cloud eval fallback)
- [x] Error display on download failure
- [x] Auto-skip if already installed

### 6. Stockfish lifecycle
- [x] StartAsync on chess agent selection (when installed)
- [x] StopAsync on disconnect (in MainViewModel.StopSessionAsync)
- [x] IStockfishService injected into AgentSelectionViewModel and MainViewModel
- [x] DI registration: MockStockfishService (dev) vs StockfishService (production)

### 7. Integration tests
- [x] 7 end-to-end tests with MockStockfishService
- [x] 6 live engine tests with real Stockfish 18 binary
- [x] Build verification: net8.0 + net8.0-maccatalyst both clean

## Test Results
279/279 passing (127 new in Phase 09)

## Commits
- Plan 09-01: (prior session) — IStockfishService foundation
- `ed08f67` — feat(09-02): dual chess tools + ToolExecutor integration
- `8f91b08` — feat(09-03): agent chess prompts, download UX, Stockfish lifecycle
- `a7420ba` — feat(09-04): integration tests + live Stockfish verification
