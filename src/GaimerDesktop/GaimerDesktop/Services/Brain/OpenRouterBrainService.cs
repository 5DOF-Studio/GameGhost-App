using System.Diagnostics;
using System.Threading.Channels;
using GaimerDesktop.Models;

namespace GaimerDesktop.Services.Brain;

/// <summary>
/// Production IBrainService backed by OpenRouter REST API.
/// Analyzes captured game images via vision models and handles multi-turn tool calling.
/// Results are published to a bounded Channel for downstream consumption by BrainEventRouter.
/// </summary>
public sealed class OpenRouterBrainService : IBrainService
{
    private readonly OpenRouterClient _client;
    private readonly ToolExecutor _toolExecutor;
    private readonly ISessionManager _sessionManager;
    private readonly Channel<BrainResult> _channel;
    private readonly string _brainModel;
    private readonly string _workerModel;
    private CancellationTokenSource _globalCts = new();
    private int _activeTasks;

    private const int MaxToolTurns = 5;

    /// <summary>
    /// Creates a new OpenRouterBrainService.
    /// </summary>
    /// <param name="client">OpenRouter HTTP client for API calls.</param>
    /// <param name="toolExecutor">Local tool call executor for multi-turn interactions.</param>
    /// <param name="sessionManager">Session state for game context.</param>
    /// <param name="brainModel">Vision model for image analysis (default: claude-sonnet-4-20250514).</param>
    /// <param name="workerModel">Lighter model for text queries (default: gpt-4o-mini).</param>
    public OpenRouterBrainService(
        OpenRouterClient client,
        ToolExecutor toolExecutor,
        ISessionManager sessionManager,
        string brainModel = "anthropic/claude-sonnet-4",
        string workerModel = "openai/gpt-4o-mini")
    {
        _client = client;
        _toolExecutor = toolExecutor;
        _sessionManager = sessionManager;
        _brainModel = brainModel;
        _workerModel = workerModel;

        _channel = Channel.CreateBounded<BrainResult>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
    }

    // ── IBrainService Properties ─────────────────────────────────────────────

    public ChannelReader<BrainResult> Results => _channel.Reader;

    public bool IsBusy => Volatile.Read(ref _activeTasks) > 0;

    public string ProviderName => $"OpenRouter ({_brainModel})";

    // ── Image Analysis ───────────────────────────────────────────────────────

    public Task SubmitImageAsync(byte[] imageData, string context, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);
        var linkedCt = linkedCts.Token;
        Interlocked.Increment(ref _activeTasks);

        _ = Task.Run(async () =>
        {
            try
            {
                var correlationId = Guid.NewGuid().ToString("N")[..8];
                Debug.WriteLine($"[Brain] SubmitImage started (correlation={correlationId}, {imageData.Length} bytes)");

                // Build context prompt with agent personality
                var gameType = _sessionManager.Context.GameType ?? "unknown";
                var personalityPrefix = GetBrainPersonality();
                var contextPrompt = $"{personalityPrefix} Analyze this game screenshot and provide tactical insights. Current game: {gameType}. {context}";

                // Build request with vision
                var request = _client.CreateImageAnalysisRequest(imageData, contextPrompt, _brainModel);

                // Add tool definitions if available
                var tools = ToolExecutor.GetAvailableToolDefinitions(_sessionManager);
                if (tools.Count > 0)
                {
                    request.Tools = tools;
                    request.ToolChoice = "auto";
                }

                // Execute initial request
                var messages = new List<OpenRouterMessage>(request.Messages);
                var response = await _client.ChatCompletionAsync(request, linkedCt).ConfigureAwait(false);

                // Tool call loop (max MaxToolTurns iterations)
                int toolTurns = 0;
                while (response.Choices?.Count > 0
                       && response.Choices[0].FinishReason == "tool_calls"
                       && toolTurns < MaxToolTurns)
                {
                    var assistantMsg = response.Choices[0].Message;
                    messages.Add(assistantMsg);

                    // Execute each tool call
                    foreach (var toolCall in assistantMsg.ToolCalls ?? [])
                    {
                        Debug.WriteLine($"[Brain] Tool call: {toolCall.Function.Name} (turn {toolTurns + 1})");
                        var toolResult = await _toolExecutor.ExecuteToolAsync(
                            toolCall.Function.Name, toolCall.Function.Arguments, linkedCt)
                            .ConfigureAwait(false);
                        messages.Add(new OpenRouterMessage
                        {
                            Role = "tool",
                            Content = toolResult,
                            ToolCallId = toolCall.Id
                        });
                    }

                    // Send tool results back to LLM
                    request.Messages = messages;
                    request.Tools = tools; // Keep tools available
                    response = await _client.ChatCompletionAsync(request, linkedCt).ConfigureAwait(false);
                    toolTurns++;
                }

                // Extract final response
                var finalContent = response.Choices?.FirstOrDefault()?.Message?.Content?.ToString();
                Debug.WriteLine($"[Brain] Image analysis complete (correlation={correlationId}, toolTurns={toolTurns})");

                // Write result to channel
                await _channel.Writer.WriteAsync(new BrainResult
                {
                    Type = BrainResultType.ImageAnalysis,
                    AnalysisText = finalContent ?? "No analysis available",
                    VoiceNarration = TruncateForVoice(finalContent, 200),
                    Priority = BrainResultPriority.WhenIdle,
                    CorrelationId = correlationId,
                }, linkedCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Brain] Image analysis cancelled");
            }
            catch (OpenRouterException orEx)
            {
                Debug.WriteLine($"[Brain] Image analysis failed: {orEx.StatusCode} — {orEx.ResponseBody}");
                // Show status code + model, but not the full response body (may contain reflected auth data)
                await _channel.Writer.WriteAsync(new BrainResult
                {
                    Type = BrainResultType.Error,
                    AnalysisText = $"Brain error: HTTP {(int)orEx.StatusCode} from model {_brainModel}",
                    Priority = BrainResultPriority.Silent,
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Brain] Image analysis failed: {ex.Message}");
                await _channel.Writer.WriteAsync(new BrainResult
                {
                    Type = BrainResultType.Error,
                    AnalysisText = $"Brain error: {ex.GetType().Name} — {ex.Message}",
                    Priority = BrainResultPriority.Silent,
                }, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
                linkedCts.Dispose();
            }
        }, linkedCt);

        return Task.CompletedTask;
    }

    // ── Text Query ───────────────────────────────────────────────────────────

    public Task SubmitQueryAsync(string userQuery, SharedContextEnvelope context, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);
        var linkedCt = linkedCts.Token;
        Interlocked.Increment(ref _activeTasks);

        _ = Task.Run(async () =>
        {
            try
            {
                var correlationId = Guid.NewGuid().ToString("N")[..8];
                Debug.WriteLine($"[Brain] SubmitQuery started (correlation={correlationId})");

                // Build context from envelope with agent personality
                var personalityPrefix = GetBrainPersonality();
                var systemContext = $"{personalityPrefix} Game state: {_sessionManager.Context.State}. " +
                                    $"Game: {_sessionManager.Context.GameType ?? "unknown"}. " +
                                    $"Intent: {context.Intent}. " +
                                    (string.IsNullOrEmpty(context.RollingSummary)
                                        ? ""
                                        : $"Summary: {context.RollingSummary}");

                var messages = new List<OpenRouterMessage>
                {
                    new() { Role = "system", Content = systemContext },
                    new() { Role = "user", Content = userQuery }
                };

                var request = new OpenRouterRequest
                {
                    Model = _workerModel,
                    Messages = messages,
                    MaxTokens = 512
                };

                // Add tool definitions if available
                var tools = ToolExecutor.GetAvailableToolDefinitions(_sessionManager);
                if (tools.Count > 0)
                {
                    request.Tools = tools;
                    request.ToolChoice = "auto";
                }

                var response = await _client.ChatCompletionAsync(request, linkedCt).ConfigureAwait(false);

                // Tool call loop
                int toolTurns = 0;
                while (response.Choices?.Count > 0
                       && response.Choices[0].FinishReason == "tool_calls"
                       && toolTurns < MaxToolTurns)
                {
                    var assistantMsg = response.Choices[0].Message;
                    messages.Add(assistantMsg);

                    foreach (var toolCall in assistantMsg.ToolCalls ?? [])
                    {
                        Debug.WriteLine($"[Brain] Query tool call: {toolCall.Function.Name} (turn {toolTurns + 1})");
                        var toolResult = await _toolExecutor.ExecuteToolAsync(
                            toolCall.Function.Name, toolCall.Function.Arguments, linkedCt)
                            .ConfigureAwait(false);
                        messages.Add(new OpenRouterMessage
                        {
                            Role = "tool",
                            Content = toolResult,
                            ToolCallId = toolCall.Id
                        });
                    }

                    request.Messages = messages;
                    request.Tools = tools;
                    response = await _client.ChatCompletionAsync(request, linkedCt).ConfigureAwait(false);
                    toolTurns++;
                }

                var finalContent = response.Choices?.FirstOrDefault()?.Message?.Content?.ToString();
                Debug.WriteLine($"[Brain] Query complete (correlation={correlationId}, toolTurns={toolTurns})");

                await _channel.Writer.WriteAsync(new BrainResult
                {
                    Type = BrainResultType.ToolResult,
                    AnalysisText = finalContent ?? "No response available",
                    VoiceNarration = TruncateForVoice(finalContent, 200),
                    Priority = BrainResultPriority.WhenIdle,
                    CorrelationId = correlationId,
                }, linkedCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Brain] Query cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Brain] Query failed: {ex.Message}");
                await _channel.Writer.WriteAsync(new BrainResult
                {
                    Type = BrainResultType.Error,
                    AnalysisText = "Query processing failed. Check logs for details.",
                    Priority = BrainResultPriority.Silent,
                }, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
                linkedCts.Dispose();
            }
        }, linkedCt);

        return Task.CompletedTask;
    }

    // ── Direct Chat (request-reply) ────────────────────────────────────────

    public async Task<string> ChatAsync(string userQuery, IReadOnlyList<ChatMessage> chatHistory, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);
        Interlocked.Increment(ref _activeTasks);
        try
        {
            var personalityPrefix = GetBrainPersonality();
            var systemPrompt = $"{personalityPrefix} You are chatting with the user outside of a game session. " +
                               "Be helpful, conversational, and stay in character. " +
                               "You can discuss strategy, past games, or general gaming topics.";

            var messages = new List<OpenRouterMessage>
            {
                new() { Role = "system", Content = systemPrompt }
            };

            // Add recent chat history for conversational context
            foreach (var msg in chatHistory.TakeLast(10))
            {
                if (msg.Role == MessageRole.User)
                    messages.Add(new OpenRouterMessage { Role = "user", Content = msg.Content });
                else if (msg.Role == MessageRole.Assistant)
                    messages.Add(new OpenRouterMessage { Role = "assistant", Content = msg.Content });
            }

            messages.Add(new OpenRouterMessage { Role = "user", Content = userQuery });

            var request = new OpenRouterRequest
            {
                Model = _workerModel,
                Messages = messages,
                MaxTokens = 512
            };

            var response = await _client.ChatCompletionAsync(request, linkedCts.Token).ConfigureAwait(false);
            var reply = response.Choices?.FirstOrDefault()?.Message?.Content?.ToString();

            Debug.WriteLine($"[Brain] ChatAsync complete — {reply?.Length ?? 0} chars");
            return reply ?? "I'm not sure how to respond to that right now.";
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[Brain] ChatAsync cancelled");
            return "Chat was cancelled.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Brain] ChatAsync failed: {ex.Message}");
            return "Sorry, I couldn't process that right now. Try again in a moment.";
        }
        finally
        {
            Interlocked.Decrement(ref _activeTasks);
            linkedCts.Dispose();
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void CancelAll()
    {
        Debug.WriteLine("[Brain] CancelAll — cancelling all in-flight requests");
        var oldCts = Interlocked.Exchange(ref _globalCts, new CancellationTokenSource());
        try { oldCts.Cancel(); } catch { /* already disposed */ }
        oldCts.Dispose();
    }

    public void Dispose()
    {
        CancelAll();
        _channel.Writer.TryComplete();
        _globalCts.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current agent's BrainPersonalityPrefix, or a generic fallback.
    /// </summary>
    private string GetBrainPersonality()
    {
        var agentKey = _sessionManager.Context.AgentKey;
        if (!string.IsNullOrEmpty(agentKey))
        {
            var agent = Agents.GetByKey(agentKey);
            if (agent?.BrainPersonalityPrefix is not null)
                return agent.BrainPersonalityPrefix.Trim();
        }

        return "You are a gaming AI analyst.";
    }

    /// <summary>
    /// Truncates text for voice narration. Prefers sentence boundary if possible.
    /// </summary>
    private static string TruncateForVoice(string? text, int maxLen)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= maxLen)
            return text;

        // Try to truncate at a sentence boundary
        var searchRegion = text[..maxLen];
        var lastPeriod = searchRegion.LastIndexOf(". ", StringComparison.Ordinal);
        if (lastPeriod > maxLen / 2) // Only if we keep at least half the content
            return text[..(lastPeriod + 1)];

        var lastQuestion = searchRegion.LastIndexOf('?');
        if (lastQuestion > maxLen / 2)
            return text[..(lastQuestion + 1)];

        var lastExclaim = searchRegion.LastIndexOf('!');
        if (lastExclaim > maxLen / 2)
            return text[..(lastExclaim + 1)];

        // Fall back to hard truncate
        return text[..(maxLen - 3)] + "...";
    }
}
