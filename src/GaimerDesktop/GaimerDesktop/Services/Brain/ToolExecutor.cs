using System.Text.Json;
using Microsoft.Extensions.Logging;
using GaimerDesktop.Models;
using GaimerDesktop.Services.Chess;

namespace GaimerDesktop.Services.Brain;

/// <summary>
/// Executes local tool calls requested by the brain LLM during multi-turn analysis.
/// Maps tool names to local service calls and returns JSON string results for the LLM.
/// </summary>
public sealed class ToolExecutor
{
    private readonly IWindowCaptureService _captureService;
    private readonly ISessionManager _sessionManager;
    private readonly OpenRouterClient _openRouterClient;
    private readonly HttpClient _lichessClient;
    private readonly IStockfishService _stockfishService;
    private readonly string _workerModel;
    private readonly ILogger<ToolExecutor> _logger;

    public ToolExecutor(
        IWindowCaptureService captureService,
        ISessionManager sessionManager,
        OpenRouterClient openRouterClient,
        HttpClient lichessClient,
        IStockfishService stockfishService,
        string workerModel,
        ILogger<ToolExecutor> logger)
    {
        _captureService = captureService;
        _sessionManager = sessionManager;
        _openRouterClient = openRouterClient;
        _lichessClient = lichessClient;
        _stockfishService = stockfishService;
        _workerModel = workerModel;
        _logger = logger;
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

        try
        {
            return toolName switch
            {
                "capture_screen" => await ExecuteCaptureScreenAsync(ct),
                "get_game_state" => ExecuteGetGameState(),
                "analyze_position_engine" => await ExecuteAnalyzePositionEngineAsync(argumentsJson, ct),
                "analyze_position_strategic" => await ExecuteAnalyzePositionStrategicAsync(argumentsJson, ct),
                "get_best_move" => await ExecuteGetBestMoveAsync(argumentsJson, ct),
                "web_search" => await ExecuteWebSearchAsync(argumentsJson, ct),
                "player_history" => JsonSerializer.Serialize(new { status = "not_available", reason = "Player history requires persistence layer (Phase 07)" }),
                "player_analytics" => JsonSerializer.Serialize(new { status = "not_available", reason = "Player analytics requires persistence layer (Phase 07)" }),
                _ => JsonSerializer.Serialize(new { error = "Unknown tool", tool_name = toolName })
            };
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation propagate
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToolExecutor] Error executing tool {ToolName}", toolName);
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

    // ── analyze_position_engine (Stockfish with Lichess fallback) ─────────────

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

        // Try Stockfish first, fall back to Lichess if not ready
        if (!_stockfishService.IsReady)
        {
            _logger.LogDebug("[ToolExecutor] Stockfish not ready, falling back to Lichess cloud eval");
            return await ExecuteGetBestMoveAsync(argumentsJson, ct);
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
            _logger.LogWarning(ex, "[ToolExecutor] Stockfish analysis failed, falling back to Lichess");
            return await ExecuteGetBestMoveAsync(argumentsJson, ct);
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

    // ── get_best_move (Lichess cloud eval — legacy fallback) ──────────────────

    private async Task<string> ExecuteGetBestMoveAsync(string argumentsJson, CancellationToken ct)
    {
        string? fen = null;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("fen", out var fenProp))
                fen = fenProp.GetString();
        }
        catch (JsonException) { }

        if (string.IsNullOrWhiteSpace(fen))
        {
            return JsonSerializer.Serialize(new
            {
                status = "not_available",
                reason = "Missing required 'fen' parameter"
            });
        }

        try
        {
            var encodedFen = Uri.EscapeDataString(fen);
            var url = $"api/cloud-eval?fen={encodedFen}&multiPv=1";

            HttpResponseMessage? response = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    response = await _lichessClient.GetAsync(url, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    if (attempt == 0)
                    {
                        _logger.LogDebug("[ToolExecutor] Lichess timeout, retrying once");
                        continue;
                    }
                    return JsonSerializer.Serialize(new
                    {
                        status = "not_available",
                        reason = "Chess evaluation service timed out"
                    });
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt == 0)
                {
                    _logger.LogDebug("[ToolExecutor] Lichess rate-limited, retrying after 1s");
                    response.Dispose();
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                    continue;
                }
                break;
            }

            if (response is null || !response.IsSuccessStatusCode)
            {
                var statusCode = response?.StatusCode;
                response?.Dispose();
                _logger.LogWarning("[ToolExecutor] Lichess cloud eval returned {Status}", statusCode);
                return JsonSerializer.Serialize(new
                {
                    status = "not_available",
                    reason = $"Lichess returned {(int)(statusCode ?? 0)}"
                });
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var evalDoc = JsonDocument.Parse(json);
            var root = evalDoc.RootElement;

            var evalDepth = root.TryGetProperty("depth", out var depthProp) ? depthProp.GetInt32() : 0;

            if (!root.TryGetProperty("pvs", out var pvs) || pvs.GetArrayLength() == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "not_available",
                    reason = "No analysis available for this position"
                });
            }

            var bestPv = pvs[0];
            var moves = bestPv.TryGetProperty("moves", out var movesProp) ? movesProp.GetString() : "";
            var bestMove = moves?.Split(' ').FirstOrDefault() ?? "";

            int? evaluation = null;
            string signal;
            string assessment;

            if (bestPv.TryGetProperty("mate", out var mateProp))
            {
                var mateIn = mateProp.GetInt32();
                evaluation = mateIn > 0 ? 10000 : -10000;
                signal = mateIn > 0 ? "opportunity" : "blunder";
                assessment = mateIn > 0 ? $"Mate in {mateIn}" : $"Getting mated in {Math.Abs(mateIn)}";
            }
            else if (bestPv.TryGetProperty("cp", out var cpProp))
            {
                var cp = cpProp.GetInt32();
                evaluation = cp;
                (signal, assessment) = ClassifyEvaluation(cp);
            }
            else
            {
                return JsonSerializer.Serialize(new
                {
                    status = "not_available",
                    reason = "Evaluation data missing from response"
                });
            }

            _logger.LogDebug("[ToolExecutor] get_best_move: eval={Eval} best={Move} depth={Depth}",
                evaluation, bestMove, evalDepth);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                evaluation,
                assessment,
                bestMove,
                depth = evalDepth,
                signal
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ToolExecutor] Lichess cloud eval failed");
            return JsonSerializer.Serialize(new
            {
                status = "not_available",
                reason = "Chess evaluation service unavailable"
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
