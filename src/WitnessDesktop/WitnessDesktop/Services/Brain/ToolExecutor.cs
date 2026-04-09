using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Services.Replay;
using WitnessDesktop.Services;

namespace WitnessDesktop.Services.Brain;

/// <summary>
/// Executes local tool calls requested by the brain LLM during multi-turn analysis.
/// Maps tool names to local service calls and returns JSON string results for the LLM.
/// </summary>
public sealed class ToolExecutor
{
    private readonly IWindowCaptureService _captureService;
    private readonly ISessionManager _sessionManager;
    private readonly OpenRouterClient _openRouterClient;
    private readonly IStockfishService _stockfishService;
    private readonly string _workerModel;
    private readonly ILogger<ToolExecutor> _logger;
    private readonly ITelemetryService? _telemetry;
    private readonly IGameJournalService? _gameJournal;
    private readonly ISessionTraceService? _sessionTrace;
    private readonly ISegmentAnalysisStore? _segmentAnalysisStore;
    private readonly IVideoAnalysisTool? _videoAnalysisTool;
    private readonly IReplayRecordingService? _replayRecording;
    private readonly IGameSkillPackService? _packService;
    private readonly IGaimerTeamService? _gaimerTeam;
    private readonly IBrainContextService? _brainContext;

    public ToolExecutor(
        IWindowCaptureService captureService,
        ISessionManager sessionManager,
        OpenRouterClient openRouterClient,
        IStockfishService stockfishService,
        string workerModel,
        ILogger<ToolExecutor> logger,
        ITelemetryService? telemetry = null,
        IGameJournalService? gameJournal = null,
        ISessionTraceService? sessionTrace = null,
        ISegmentAnalysisStore? segmentAnalysisStore = null,
        IVideoAnalysisTool? videoAnalysisTool = null,
        IReplayRecordingService? replayRecording = null,
        IGameSkillPackService? packService = null,
        IGaimerTeamService? gaimerTeam = null,
        IBrainContextService? brainContext = null)
    {
        _captureService = captureService;
        _sessionManager = sessionManager;
        _openRouterClient = openRouterClient;
        _stockfishService = stockfishService;
        _workerModel = workerModel;
        _logger = logger;
        _telemetry = telemetry;
        _gameJournal = gameJournal;
        _sessionTrace = sessionTrace;
        _segmentAnalysisStore = segmentAnalysisStore;
        _videoAnalysisTool = videoAnalysisTool;
        _replayRecording = replayRecording;
        _packService = packService;
        _gaimerTeam = gaimerTeam;
        _brainContext = brainContext;
    }

    // ── Tool Dispatch ────────────────────────────────────────────────────────

    /// <summary>
    /// Execute a tool by name, returning a JSON string result for the LLM.
    /// All tool results are JSON objects — the LLM parses them in the next turn.
    /// </summary>
    public async Task<string> ExecuteToolAsync(
        string toolName,
        string argumentsJson,
        CancellationToken ct = default)
    {
        _logger.LogDebug("[ToolExecutor] Executing tool: {ToolName} args={Args}", toolName, argumentsJson);
        _telemetry?.TrackEvent("tool", "called", new Dictionary<string, string>
        {
            ["toolName"] = toolName
        });
        _sessionTrace?.TrackEvent("brain.tool_call", new Dictionary<string, string>
        {
            ["tool_name"] = toolName
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = toolName switch
            {
                "capture_screen" => await ExecuteCaptureScreenAsync(ct),
                "get_game_state" => ExecuteGetGameState(),
                "analyze_position_engine" => await ExecuteAnalyzePositionEngineAsync(argumentsJson, ct),
                "analyze_position_strategic" => await ExecuteAnalyzePositionStrategicAsync(argumentsJson, ct),
                "game_journal" => ExecuteGameJournal(argumentsJson),
                "web_search" => await ExecuteWebSearchAsync(argumentsJson, ct),
                "search_replay" => await ExecuteSearchReplayAsync(argumentsJson, ct),
                "show_replay" => await ExecuteShowReplayAsync(argumentsJson, ct),
                "delegate_to_team" => await ExecuteDelegateToTeamAsync(argumentsJson, ct),
                _ => JsonSerializer.Serialize(new { error = "Unknown tool", tool_name = toolName })
            };
            sw.Stop();
            _telemetry?.TrackEvent("tool", "completed", new Dictionary<string, string>
            {
                ["toolName"] = toolName,
                ["duration_ms"] = sw.ElapsedMilliseconds.ToString()
            });
            _sessionTrace?.TrackEvent("brain.tool_result", new Dictionary<string, string>
            {
                ["tool_name"] = toolName,
                ["duration_ms"] = sw.ElapsedMilliseconds.ToString(),
                ["success"] = "true",
                ["result_length"] = result.Length.ToString()
            });
            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _sessionTrace?.TrackEvent("brain.tool_result", new Dictionary<string, string>
            {
                ["tool_name"] = toolName,
                ["duration_ms"] = sw.ElapsedMilliseconds.ToString(),
                ["success"] = "false",
                ["error_type"] = "cancelled"
            });
            throw; // Let cancellation propagate
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[ToolExecutor] Error executing tool {ToolName}", toolName);
            _telemetry?.TrackEvent("tool", "failed", new Dictionary<string, string>
            {
                ["toolName"] = toolName,
                ["error"] = ex.GetType().Name,
                ["duration_ms"] = sw.ElapsedMilliseconds.ToString()
            });
            _sessionTrace?.TrackEvent("brain.tool_result", new Dictionary<string, string>
            {
                ["tool_name"] = toolName,
                ["duration_ms"] = sw.ElapsedMilliseconds.ToString(),
                ["success"] = "false",
                ["error_type"] = ex.GetType().Name
            });
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    // ── capture_screen ───────────────────────────────────────────────────────

    private async Task<string> ExecuteCaptureScreenAsync(CancellationToken ct)
    {
        if (!_captureService.IsCapturing)
        {
            return JsonSerializer.Serialize(new { error = "Not currently capturing any window" });
        }

        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnFrameCaptured(object? sender, byte[] frameData)
        {
            tcs.TrySetResult(frameData);
        }

        _captureService.FrameCaptured += OnFrameCaptured;
        try
        {
            // Wait for the next frame with a 5-second timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            using var reg = timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token));

            byte[] imageData;
            try
            {
                imageData = await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout, not external cancellation
                return JsonSerializer.Serialize(new { error = "Capture timed out" });
            }

            var processName = _captureService.CurrentTarget?.ProcessName ?? "unknown";
            var base64 = Convert.ToBase64String(imageData);

            _logger.LogDebug("[ToolExecutor] capture_screen: {Bytes} bytes from {Process}",
                imageData.Length, processName);

            return JsonSerializer.Serialize(new
            {
                status = "captured",
                image_base64 = base64,
                target = processName
            });
        }
        finally
        {
            _captureService.FrameCaptured -= OnFrameCaptured;
        }
    }

    // ── get_game_state ───────────────────────────────────────────────────────

    private string ExecuteGetGameState()
    {
        var ctx = _sessionManager.Context;
        var availableTools = _sessionManager.GetAvailableTools();

        return JsonSerializer.Serialize(new
        {
            state = ctx.State.ToString(),
            game_id = ctx.GameId,
            game_type = ctx.GameType,
            connector_name = ctx.ConnectorName,
            game_started_at = ctx.GameStartedAt?.ToString("o"),
            available_tools = availableTools.Select(t => t.Name).ToList()
        });
    }

    // ── analyze_position_engine (Stockfish only — no fallback) ────────────────

    private async Task<string> ExecuteAnalyzePositionEngineAsync(string argumentsJson, CancellationToken ct)
    {
        string? fen = null;
        int depth = 25;
        int numLines = 3;

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("fen", out var fenProp))
                fen = fenProp.GetString();
            if (doc.RootElement.TryGetProperty("depth", out var depthProp))
                depth = depthProp.GetInt32();
            if (doc.RootElement.TryGetProperty("num_lines", out var linesProp))
                numLines = Math.Clamp(linesProp.GetInt32(), 1, 5);
        }
        catch (JsonException) { }

        if (string.IsNullOrWhiteSpace(fen))
        {
            return JsonSerializer.Serialize(new
            {
                status = "fen_invalid",
                errors = new[] { "Missing required 'fen' parameter" },
                hint = "Re-examine the board and provide a valid FEN string"
            });
        }

        // Validate FEN before sending to engine
        if (!FenValidator.IsValid(fen, out var fenErrors))
        {
            return JsonSerializer.Serialize(new
            {
                status = "fen_invalid",
                errors = fenErrors,
                hint = "Re-examine the board and correct the FEN string"
            });
        }

        // Stockfish must be running — no fallback
        if (!_stockfishService.IsReady)
        {
            _logger.LogWarning("[ToolExecutor] Stockfish not ready — returning error (no external fallback)");
            return JsonSerializer.Serialize(new
            {
                status = "stockfish_not_ready",
                reason = "Stockfish engine is not running. Please ensure Chess Skills are downloaded via Agent Selection.",
                hint = "Try again after Stockfish finishes loading"
            });
        }

        try
        {
            var options = new AnalysisOptions(MoveTimeMs: 1000, MaxDepth: depth, MultiPv: numLines);
            var analysis = await _stockfishService.AnalyzePositionAsync(fen, options, ct);

            // Build evaluation object
            object evaluation;
            string signal;
            string assessment;

            var bestVariation = analysis.Variations.FirstOrDefault();
            if (bestVariation?.MateIn is { } mateIn)
            {
                evaluation = new { type = "mate", value = mateIn };
                signal = mateIn > 0 ? "opportunity" : "blunder";
                assessment = mateIn > 0 ? $"Mate in {mateIn}" : $"Getting mated in {Math.Abs(mateIn)}";
            }
            else
            {
                var cp = bestVariation?.CentipawnEval ?? 0;
                evaluation = new { type = "cp", value = cp };
                (signal, assessment) = ClassifyEvaluation(cp);
            }

            // Build candidates array
            var candidates = analysis.Variations.Select(v => new
            {
                move = v.Move,
                eval = v.MateIn.HasValue ? (object)new { mate = v.MateIn.Value } : v.CentipawnEval ?? 0,
                line = string.Join(" ", v.PrincipalVariation),
                assessment = v.MateIn.HasValue
                    ? (v.MateIn > 0 ? $"Mate in {v.MateIn}" : $"Getting mated in {Math.Abs(v.MateIn.Value)}")
                    : ClassifyEvaluation(v.CentipawnEval ?? 0).assessment
            }).ToList();

            _logger.LogDebug("[ToolExecutor] analyze_position_engine: best={Move} depth={Depth} candidates={Count}",
                analysis.BestMove, analysis.Depth, candidates.Count);
            _telemetry?.TrackEvent("stockfish", "analysis_complete", new Dictionary<string, string>
            {
                ["depth"] = analysis.Depth.ToString(),
                ["best_move"] = analysis.BestMove ?? "none",
                ["eval"] = (bestVariation?.CentipawnEval?.ToString() ?? bestVariation?.MateIn?.ToString() ?? "0"),
                ["duration_ms"] = analysis.TimeMs.ToString()
            });

            return JsonSerializer.Serialize(new
            {
                status = "success",
                bestMove = analysis.BestMove,
                evaluation,
                assessment,
                signal,
                depth = analysis.Depth,
                candidates
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToolExecutor] Stockfish analysis failed — returning error (no external fallback)");
            return JsonSerializer.Serialize(new
            {
                status = "stockfish_not_ready",
                reason = "Stockfish engine encountered an error during analysis.",
                hint = "Try again after Stockfish finishes loading"
            });
        }
    }

    // ── analyze_position_strategic (LLM-based) ───────────────────────────────

    private async Task<string> ExecuteAnalyzePositionStrategicAsync(string argumentsJson, CancellationToken ct)
    {
        string focus = "general";
        string playerColor = "unknown";

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("focus", out var focusProp))
                focus = focusProp.GetString() ?? "general";
            if (doc.RootElement.TryGetProperty("player_color", out var colorProp))
                playerColor = colorProp.GetString() ?? "unknown";
        }
        catch (JsonException) { }

        try
        {
            var prompt = $"""
                You are a chess analysis engine. Analyze the current position strategically.
                Focus: {focus}
                Player color: {playerColor}

                Provide:
                1. Key strategic themes (2-3 bullet points)
                2. Piece activity assessment (which pieces are active/passive)
                3. Pawn structure evaluation
                4. Recommended plan (1-2 sentences)
                5. King safety assessment

                Keep analysis concise — this feeds a voice coaching agent.
                """;

            var request = new OpenRouterRequest
            {
                Model = _workerModel,
                Messages = new List<OpenRouterMessage>
                {
                    new() { Role = "system", Content = prompt },
                    new() { Role = "user", Content = $"Analyze the current chess position. Focus: {focus}. Player is {playerColor}." }
                },
                MaxTokens = 512,
                Temperature = 0.3
            };

            var response = await _openRouterClient.ChatCompletionAsync(request, ct).ConfigureAwait(false);
            var analysis = response.Choices.FirstOrDefault()?.Message.Content as string;

            if (string.IsNullOrWhiteSpace(analysis))
            {
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    reason = "No strategic analysis generated"
                });
            }

            _logger.LogDebug("[ToolExecutor] analyze_position_strategic: focus={Focus} len={Len}",
                focus, analysis.Length);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                focus,
                player_color = playerColor,
                analysis
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ToolExecutor] Strategic analysis failed");
            return JsonSerializer.Serialize(new
            {
                status = "error",
                reason = "Strategic analysis unavailable"
            });
        }
    }

    private static (string signal, string assessment) ClassifyEvaluation(int centipawns)
    {
        return centipawns switch
        {
            > 500 => ("opportunity", "Winning"),
            > 200 => ("opportunity", "Clear advantage"),
            > 100 => ("opportunity", "Slight advantage"),
            > -100 => ("neutral", "Equal"),
            > -200 => ("blunder", "Slight disadvantage"),
            > -500 => ("blunder", "Clear disadvantage"),
            _ => ("blunder", "Losing")
        };
    }

    // ── game_journal ────────────────────────────────────────────────────────

    private string ExecuteGameJournal(string argumentsJson)
    {
        string? action = null;
        string? entryType = null;
        string? content = null;
        string[]? tags = null;

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("action", out var actionProp))
                action = actionProp.GetString();
            if (doc.RootElement.TryGetProperty("entry_type", out var typeProp))
                entryType = typeProp.GetString();
            if (doc.RootElement.TryGetProperty("content", out var contentProp))
                content = contentProp.GetString();
            if (doc.RootElement.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
                tags = tagsProp.EnumerateArray().Select(t => t.GetString() ?? "").ToArray();
        }
        catch (JsonException) { }

        if (action is null)
            return ExecuteChessJournalReadOnly();

        return action switch
        {
            "add" => ExecuteJournalAdd(entryType, content, tags),
            "query" => ExecuteJournalQuery(content),
            "summary" => ExecuteJournalSummary(),
            _ => JsonSerializer.Serialize(new { status = "error", reason = $"Unknown journal action: {action}" })
        };
    }

    private string ExecuteChessJournalReadOnly()
    {
        if (_gameJournal == null)
            return JsonSerializer.Serialize(new { status = "not_available", reason = "Game journal not initialized" });

        var entries = _gameJournal.GetEntries();
        return JsonSerializer.Serialize(new
        {
            status = "success",
            entry_count = entries.Count,
            entries = entries.Select(e => new
            {
                move = e.MoveNumber,
                fen = e.Fen,
                notation = e.MoveNotation,
                description = e.Description,
                eval = e.Evaluation,
                time = e.Timestamp.ToString("HH:mm:ss")
            }).ToList(),
            summary = _gameJournal.GetSummary()
        });
    }

    private string ExecuteJournalAdd(string? entryType, string? content, string[]? tags)
    {
        if (string.IsNullOrWhiteSpace(content))
            return JsonSerializer.Serialize(new { status = "error", reason = "Missing 'content' for journal entry" });

        _sessionTrace?.TrackEvent("rasa.journal_entry", new Dictionary<string, string>
        {
            ["entry_type"] = entryType ?? "observation",
            ["content"] = content,
            ["tags"] = tags is not null ? string.Join(",", tags) : ""
        });

        _logger.LogDebug("[ToolExecutor] game_journal add: type={Type} content_len={Len}",
            entryType ?? "observation", content.Length);

        return JsonSerializer.Serialize(new
        {
            status = "logged",
            entry_type = entryType ?? "observation",
            content_preview = content.Length > 80 ? content[..80] + "..." : content,
            tags = tags ?? Array.Empty<string>(),
            note = "Entry recorded. Knowledge base integration coming soon."
        });
    }

    private string ExecuteJournalQuery(string? query)
    {
        _logger.LogDebug("[ToolExecutor] game_journal query: {Query}", query ?? "(empty)");
        return JsonSerializer.Serialize(new
        {
            status = "not_available",
            reason = "Knowledge base not yet connected. Journal entries are being recorded for future use.",
            query = query ?? ""
        });
    }

    private string ExecuteJournalSummary()
    {
        if (_gameJournal != null)
            return ExecuteChessJournalReadOnly();

        _logger.LogDebug("[ToolExecutor] game_journal summary requested (no chess journal)");
        return JsonSerializer.Serialize(new
        {
            status = "not_available",
            reason = "Session summary requires knowledge base integration. Journal entries are being recorded."
        });
    }

    // ── web_search (LLM knowledge) ───────────────────────────────────────────

    private async Task<string> ExecuteWebSearchAsync(string argumentsJson, CancellationToken ct)
    {
        string? query = null;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("query", out var queryProp))
                query = queryProp.GetString();
        }
        catch (JsonException) { }

        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                reason = "Missing required 'query' parameter",
                query = "(no query provided)"
            });
        }

        try
        {
            var request = new OpenRouterRequest
            {
                Model = _workerModel,
                Messages = new List<OpenRouterMessage>
                {
                    new()
                    {
                        Role = "system",
                        Content = "You are a gaming knowledge assistant. Answer the following gaming question concisely (2-3 paragraphs max). Focus on practical, actionable advice."
                    },
                    new()
                    {
                        Role = "user",
                        Content = query
                    }
                },
                MaxTokens = 512,
                Temperature = 0.3
            };

            var response = await _openRouterClient.ChatCompletionAsync(request, ct).ConfigureAwait(false);

            var answer = response.Choices.FirstOrDefault()?.Message.Content as string;

            if (string.IsNullOrWhiteSpace(answer))
            {
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    reason = "No response from knowledge base",
                    query
                });
            }

            _logger.LogDebug("[ToolExecutor] web_search: query='{Query}' answer_len={Len}",
                query, answer.Length);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                query,
                answer,
                source = "llm_knowledge"
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ToolExecutor] web_search failed for query: {Query}", query);
            return JsonSerializer.Serialize(new
            {
                status = "error",
                reason = "Knowledge search unavailable",
                query
            });
        }
    }

    // ── search_replay (Two-tier: store first, fresh analysis on miss) ────

    private async Task<string> ExecuteSearchReplayAsync(string argumentsJson, CancellationToken ct)
    {
        string? query = null;
        string? timeHint = null;

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("query", out var queryProp))
                query = queryProp.GetString();
            if (doc.RootElement.TryGetProperty("time_hint", out var timeProp))
                timeHint = timeProp.GetString();
        }
        catch (JsonException) { }

        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                reason = "Missing required 'query' parameter"
            });
        }

        // Parse time_hint into a time window for filtering
        var (startUtc, endUtc) = ParseTimeHint(timeHint);

        // ── Tier 1: Check cached analysis store (instant, free) ──
        if (_segmentAnalysisStore != null)
        {
            try
            {
                var beats = await _segmentAnalysisStore.SearchAsync(query, startUtc: startUtc, endUtc: endUtc, ct: ct);
                if (beats.Count > 0)
                {
                    _logger.LogDebug("[ToolExecutor] search_replay: store hit ({Count} beats)", beats.Count);
                    return JsonSerializer.Serialize(new
                    {
                        status = "success",
                        source = "cached",
                        query,
                        matches = beats.Select(b => new
                        {
                            start_time = b.StartTime,
                            end_time = b.EndTime,
                            assessment = b.Assessment,
                            signal = b.Signal,
                            urgency = b.Urgency
                        }).Take(10).ToList(),
                        match_count = beats.Count,
                        note = beats.Count > 10 ? $"Showing 10 of {beats.Count} matches" : null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ToolExecutor] search_replay: store search failed, falling through to fresh analysis");
            }
        }

        // ── Tier 2: Fresh Gemini analysis on available segments ──
        if (_replayRecording == null || _videoAnalysisTool == null)
        {
            return JsonSerializer.Serialize(new
            {
                status = "no_footage",
                reason = "Replay recording is not active. No footage available to search.",
                query
            });
        }

        var allSegments = _replayRecording.GetAvailableSegments();
        // Filter segments by time window when time_hint was parsed
        var segments = startUtc.HasValue
            ? allSegments.Where(s => s.EndUtc >= startUtc.Value).ToList()
            : (IReadOnlyList<ReplaySegment>)allSegments;
        if (segments.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                status = "no_footage",
                reason = "No recorded footage available. Footage covers the last 5 minutes of gameplay.",
                query
            });
        }

        if (_packService?.ActivePack is not { } pack)
        {
            return JsonSerializer.Serialize(new
            {
                status = "no_game_context",
                reason = "No game context loaded. Connect to a game first.",
                query
            });
        }

        if (_videoAnalysisTool.IsCircuitBroken)
        {
            return JsonSerializer.Serialize(new
            {
                status = "analysis_unavailable",
                reason = "Video analysis is temporarily unavailable due to rate limiting. Try again later.",
                query
            });
        }

        try
        {
            var searchResult = await _videoAnalysisTool.SearchAsync(segments, pack, query, ct);

            _logger.LogDebug("[ToolExecutor] search_replay: fresh analysis returned {Count} hits", searchResult.Hits.Count);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                source = "fresh_analysis",
                query,
                summary = searchResult.Summary,
                hits = searchResult.Hits.Select(h => new
                {
                    start_time = h.StartTime,
                    end_time = h.EndTime,
                    description = h.Description,
                    confidence = h.Confidence
                }).ToList(),
                hit_count = searchResult.Hits.Count
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ToolExecutor] search_replay: fresh analysis failed");
            return JsonSerializer.Serialize(new
            {
                status = "error",
                reason = "Couldn't review the footage right now. Try again in a moment.",
                query
            });
        }
    }

    // ── show_replay (timestamp → video card) ────────────────────────────

    private async Task<string> ExecuteShowReplayAsync(string argumentsJson, CancellationToken ct)
    {
        string? timestamp = null;
        int duration = 30;
        string? title = null;

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("timestamp", out var tsProp))
                timestamp = tsProp.GetString();
            if (doc.RootElement.TryGetProperty("duration", out var durProp))
                duration = Math.Clamp(durProp.GetInt32(), 1, 60);
            if (doc.RootElement.TryGetProperty("title", out var titleProp))
                title = titleProp.GetString();
        }
        catch (JsonException) { }

        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                reason = "Missing required 'timestamp' parameter"
            });
        }

        if (_replayRecording is null || !_replayRecording.IsRecording)
        {
            return JsonSerializer.Serialize(new
            {
                status = "no_footage",
                reason = "Replay recording is not active. No footage available."
            });
        }

        var segments = _replayRecording.GetAvailableSegments();
        if (segments.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                status = "no_footage",
                reason = "No recorded segments available yet."
            });
        }

        // Determine session start from context
        var ctx = _sessionManager.Context;
        var sessionStartUtc = ctx.GameStartedAt.HasValue
            ? new DateTimeOffset(ctx.GameStartedAt.Value, TimeSpan.Zero)
            : segments[0].StartUtc; // Fallback to first segment start

        // Try synchronous resolution first (absolute "M:SS" or relative "now-Ns")
        var resolved = ResolveTimestamp(timestamp, segments, sessionStartUtc, duration);

        // If not resolved, try anchor resolution via analysis store
        if (resolved is null && _segmentAnalysisStore is not null)
        {
            try
            {
                resolved = await ResolveTimestampWithAnchorAsync(
                    timestamp, segments, sessionStartUtc, _segmentAnalysisStore, duration, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ToolExecutor] show_replay: anchor resolution failed for '{Timestamp}'", timestamp);
            }
        }

        if (resolved is null)
        {
            _logger.LogDebug("[ToolExecutor] show_replay: could not resolve timestamp '{Timestamp}'", timestamp);
            return JsonSerializer.Serialize(new
            {
                status = "not_found",
                reason = $"Could not find footage for timestamp '{timestamp}'. Available footage covers the last ~5 minutes.",
                timestamp
            });
        }

        var (filePath, seekOffset, clampedDuration) = resolved.Value;

        _logger.LogDebug("[ToolExecutor] show_replay: resolved '{Timestamp}' → {FilePath} @ {Offset}s for {Duration}s",
            timestamp, filePath, seekOffset, clampedDuration);
        _sessionTrace?.TrackEvent("tool.show_replay.resolved", new Dictionary<string, string>
        {
            ["timestamp"] = timestamp,
            ["file_path"] = filePath,
            ["seek_offset"] = seekOffset.ToString("F1"),
            ["duration"] = clampedDuration.ToString("F1")
        });

        return JsonSerializer.Serialize(new
        {
            status = "success",
            filePath,
            startTime = seekOffset,
            duration = clampedDuration,
            title = title ?? ""
        });
    }

    // ── Time Hint Parsing ─────────────────────────────────────────────────────

    private static readonly Regex TimeHintPattern = new(
        @"(?:last|past)\s+(\d+)\s*(min(?:ute)?s?|sec(?:ond)?s?|m|s)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static (DateTimeOffset? startUtc, DateTimeOffset? endUtc) ParseTimeHint(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return (null, null);

        var now = DateTimeOffset.UtcNow;

        // "recent" / "just now" → last 2 minutes
        if (hint.Contains("recent", StringComparison.OrdinalIgnoreCase) ||
            hint.Contains("just now", StringComparison.OrdinalIgnoreCase) ||
            hint.Contains("just happened", StringComparison.OrdinalIgnoreCase))
            return (now.AddMinutes(-2), now);

        // "last N minutes/seconds" — clamped to 30 minutes (replay buffer max)
        var match = TimeHintPattern.Match(hint);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n > 0)
        {
            var unit = match.Groups[2].Value.ToLowerInvariant();
            var span = unit.StartsWith("s") ? TimeSpan.FromSeconds(Math.Min(n, 1800)) : TimeSpan.FromMinutes(Math.Min(n, 30));
            return (now - span, now);
        }

        // Unrecognized hint — don't filter, let search run against all data
        return (null, null);
    }

    // ── Timestamp Resolution (show_replay) ──────────────────────────────────

    private static readonly Regex AbsoluteTimestampPattern = new(
        @"^(\d{1,3}):(\d{2})$",
        RegexOptions.Compiled);

    private static readonly Regex RelativeTimestampPattern = new(
        @"^now-(\d+)s$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Resolve a timestamp string to a segment file path + seek offset + clamped duration.
    /// Supports absolute ("M:SS"), relative ("now-Ns"). For anchors, use ResolveTimestampWithAnchorAsync.
    /// Returns null if timestamp cannot be resolved to any available segment.
    /// </summary>
    internal static (string filePath, double seekOffset, double clampedDuration)? ResolveTimestamp(
        string timestamp,
        IReadOnlyList<ReplaySegment> segments,
        DateTimeOffset sessionStart,
        double requestedDuration = 30,
        DateTimeOffset? nowUtc = null)
    {
        if (segments.Count == 0)
            return null;

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        double? sessionRelativeSeconds = null;

        // Try absolute "M:SS"
        var absMatch = AbsoluteTimestampPattern.Match(timestamp);
        if (absMatch.Success)
        {
            var minutes = int.Parse(absMatch.Groups[1].Value);
            var seconds = int.Parse(absMatch.Groups[2].Value);
            sessionRelativeSeconds = (minutes * 60) + seconds;
        }

        // Try relative "now-Ns"
        if (sessionRelativeSeconds is null)
        {
            var relMatch = RelativeTimestampPattern.Match(timestamp);
            if (relMatch.Success)
            {
                var offsetSec = int.Parse(relMatch.Groups[1].Value);
                var sessionElapsed = (now - sessionStart).TotalSeconds;
                sessionRelativeSeconds = sessionElapsed - offsetSec;
            }
        }

        if (sessionRelativeSeconds is null || sessionRelativeSeconds < 0)
            return null;

        return FindSegmentAndOffset(segments, sessionStart, sessionRelativeSeconds.Value, requestedDuration);
    }

    /// <summary>
    /// Resolve an anchor timestamp (e.g. "last_kill") by querying the analysis store
    /// for the most recent matching event, then resolving to segment + offset.
    /// </summary>
    internal static async Task<(string filePath, double seekOffset, double clampedDuration)?> ResolveTimestampWithAnchorAsync(
        string anchor,
        IReadOnlyList<ReplaySegment> segments,
        DateTimeOffset sessionStart,
        ISegmentAnalysisStore store,
        double requestedDuration,
        CancellationToken ct)
    {
        if (segments.Count == 0)
            return null;

        var beats = await store.SearchAsync(anchor, startUtc: null, endUtc: null, ct: ct);
        if (beats.Count == 0)
            return null;

        // SearchAsync can return results in FTS relevance order, so pick the chronologically latest beat.
        var beat = beats
            .Select(beat =>
            {
                if (!TimeSpan.TryParse(beat.StartTime, out var startTime))
                    return (beat, isValid: false, startTime: TimeSpan.Zero);

                return (beat, isValid: true, startTime);
            })
            .Where(x => x.isValid)
            .OrderBy(x => x.startTime)
            .ThenBy(x => TimeSpan.TryParse(x.beat.EndTime, out var endTime) ? endTime : TimeSpan.Zero)
            .LastOrDefault().beat;

        if (beat is null)
            return null;

        // Parse beat.StartTime (format "HH:mm:ss") to session-relative seconds
        if (!TimeSpan.TryParse(beat.StartTime, out var beatTime))
            return null;

        var sessionRelativeSeconds = beatTime.TotalSeconds;
        return FindSegmentAndOffset(segments, sessionStart, sessionRelativeSeconds, requestedDuration);
    }

    private static (string filePath, double seekOffset, double clampedDuration)? FindSegmentAndOffset(
        IReadOnlyList<ReplaySegment> segments,
        DateTimeOffset sessionStart,
        double sessionRelativeSeconds,
        double requestedDuration)
    {
        // Clamp requested duration to max 60s
        var duration = Math.Min(Math.Max(requestedDuration, 1), 60);

        var targetUtc = sessionStart.AddSeconds(sessionRelativeSeconds);

        // Prefer the latest segment that starts before the timestamp and ends after it.
        // This avoids pinning exact boundary timestamps to the earlier segment.
        var segment = segments
            .Where(s => s.StartUtc <= targetUtc && s.EndUtc > targetUtc)
            .OrderByDescending(s => s.StartUtc)
            .ThenByDescending(s => s.SegmentIndex)
            .FirstOrDefault();
        if (segment is null)
            return null;

        var seekOffset = (targetUtc - segment.StartUtc).TotalSeconds;

        // Clamp duration so it doesn't exceed segment boundary
        var maxDuration = (segment.EndUtc - targetUtc).TotalSeconds;
        var clampedDuration = Math.Min(duration, maxDuration);

        return (segment.FilePath, seekOffset, clampedDuration);
    }

    // ── delegate_to_team ────────────────────────────────────────────────────

    private async Task<string> ExecuteDelegateToTeamAsync(string argumentsJson, CancellationToken ct)
    {
        if (_gaimerTeam is null || !_gaimerTeam.IsConnected)
        {
            return JsonSerializer.Serialize(new
            {
                status = "unavailable",
                reason = "Ghost Team is not connected"
            });
        }

        using var doc = JsonDocument.Parse(argumentsJson);
        var root = doc.RootElement;
        var taskText = root.GetProperty("task").GetString() ?? "Unknown task";
        var responseFormat = root.TryGetProperty("response_format", out var fmt)
            ? fmt.GetString() ?? "voice"
            : "voice";

        var ctx = _sessionManager.Context;
        var agent = ctx.AgentKey is not null ? Agents.GetByKey(ctx.AgentKey) : null;

        // Build context for team delegation
        string? l1Context = null;
        string? l2Context = null;
        string? recentActivity = null;

        if (_brainContext != null)
        {
            try
            {
                var envelope = await _brainContext.GetContextForChatAsync(
                    DateTime.UtcNow, intent: "delegation", budgetTokens: 2500, ct: ct);

                if (envelope.ImmediateEvents.Count > 0)
                    l1Context = BrainContextFormatter.FormatL1Events(envelope.ImmediateEvents);

                if (!string.IsNullOrEmpty(envelope.RollingSummary))
                    l2Context = envelope.RollingSummary;

                recentActivity = BrainContextFormatter.FormatRecentActivity(
                    envelope.RecentChatSummary, envelope.RecentVoiceTranscript);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ToolExecutor] Failed to build team context, proceeding without");
            }
        }

        var teamTask = new GaimerTeamTask
        {
            Task = taskText,
            ResponseFormat = responseFormat,
            Context = new GaimerTeamContext
            {
                Game = ctx.GameType ?? "Unknown",
                Agent = agent?.Name ?? "Unknown",
                SessionId = ctx.GameId ?? "no-session",
                L1Context = l1Context,
                L2Context = l2Context,
                RecentActivity = recentActivity
            }
        };

        var taskId = await _gaimerTeam.SubmitTaskAsync(teamTask, ct);

        return JsonSerializer.Serialize(new
        {
            status = "submitted",
            task_id = taskId
        });
    }

    // ── Tool Definition Mapping ──────────────────────────────────────────────

    /// <summary>
    /// Maps ToolDefinitions to OpenRouter function-calling tool format.
    /// Uses ParametersSchema from ToolDefinition directly.
    /// </summary>
    public static List<OpenRouterTool> GetAvailableToolDefinitions(ISessionManager sessionManager)
    {
        var availableTools = sessionManager.GetAvailableTools();
        var result = new List<OpenRouterTool>(availableTools.Count);

        foreach (var tool in availableTools)
        {
            var parametersJson = JsonDocument.Parse(tool.ParametersSchema);

            result.Add(new OpenRouterTool
            {
                Type = "function",
                Function = new OpenRouterFunction
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = parametersJson.RootElement.Clone()
                }
            });

            parametersJson.Dispose();
        }

        return result;
    }
}
