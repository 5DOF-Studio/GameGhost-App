# Summary 09-03: Agent System Prompt + Chess Skill Download UX

## Status: COMPLETE

## What Was Built

### Agent System Instructions (Models/Agent.cs)
- Added `ChessToolGuidance` constant with dual-tool guidance, FEN extraction rules, voice output conventions
- Appended to both Leroy (Chess) and Wasp (Chess Mistress) system instructions via interpolated strings
- Updated Tools lists from `["capture_screen", "get_best_move", "get_game_state", "web_search"]` to `["capture_screen", "analyze_position_engine", "analyze_position_strategic", "get_game_state", "web_search"]`
- Derek (RPG) agent unchanged — no chess tools

### Chess Skills Download UX (Views/AgentSelectionPage.xaml)
- Full-screen modal overlay with dark theme, purple accent
- Shows chess piece icon, description, size (~75 MB), license info (Stockfish/GPLv3)
- Progress bar with percentage display during download
- Error message display on failure
- DOWNLOAD button triggers `EnsureInstalledAsync` → `StartAsync` → navigate
- SKIP FOR NOW button navigates directly (Lichess cloud eval fallback)
- Uses existing `InvertedBoolConverter` and `NotNullToBool` converters

### ViewModel Changes
- **AgentSelectionViewModel**: Injected `IStockfishService`, added `ShowDownloadOverlay`, `IsDownloading`, `DownloadProgress`, `DownloadError` properties, `DownloadStockfishCommand`, `SkipDownloadCommand`
- **MainViewModel**: Injected `IStockfishService`, calls `StopAsync()` in `StopSessionAsync` when engine is running

### DI Wiring (MauiProgram.cs)
- `AgentSelectionViewModel` factory now injects `IStockfishService`
- `IStockfishService` already registered in Plan 09-02

## Tests Added
- `Chess/AgentChessTests.cs` — 11 tests: system instruction content (dual tools, FEN, voice output), Tools list validation, Derek exclusion
- `Chess/StockfishLifecycleTests.cs` — 15 tests: start/stop lifecycle, install/download, chess agent identification, interface verification

## Test Results
266/266 passing (26 new)

## Commits
- `8f91b08` — feat(09-03): agent chess prompts, download UX, Stockfish lifecycle

## Deviations
None. All plan items implemented as specified.
