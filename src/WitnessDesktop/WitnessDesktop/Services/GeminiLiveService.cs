using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// WebSocket client for Gemini Live API real-time audio/visual interaction.
/// </summary>
public sealed class GeminiLiveService : IDisposable
{
    private const string WS_URL_TEMPLATE = 
        "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={0}";
    private const int CONNECT_TIMEOUT_MS = 30_000;
    private const int RECEIVE_BUFFER_SIZE = 64 * 1024; // 64KB

    private readonly string _apiKey;
    private readonly string _voice;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoopTask;
    private Agent? _currentAgent;
    private TaskCompletionSource? _setupCompleteTcs;
    private bool _disposed;

    private string? _lastResumptionHandle;
    public string? LastResumptionHandle => _lastResumptionHandle;

    private string? _instructionsOverride;
    public Agent? CurrentAgent => _currentAgent;
    
    private volatile ConnectionState _state = ConnectionState.Disconnected;
    private readonly object _stateLock = new();
    public ConnectionState State => _state;
    public bool IsConnected => _state == ConnectionState.Connected;
    
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TextReceived;
    public event EventHandler? Interrupted;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? InputTranscriptionReceived;

    public GeminiLiveService(IConfiguration configuration, string voice = "Fenrir")
    {
        _apiKey = configuration["GeminiApiKey"]
            ?? configuration["GEMINI_APIKEY"]
            ?? configuration["GEMINI_API_KEY"]
            ?? string.Empty;
        _voice = voice;
    }
    
    public async Task ConnectAsync(Agent agent)
    {
        if (agent is null) throw new ArgumentNullException(nameof(agent));

        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;

            if (State == ConnectionState.Connected || State == ConnectionState.Connecting)
                return;

            if (string.IsNullOrEmpty(_apiKey))
            {
                SetState(ConnectionState.Error);
                ErrorOccurred?.Invoke(this, "API key not configured. See config.example.txt for setup instructions.");
                return;
            }

            _currentAgent = agent;

            // Ensure any previous connection attempt is torn down before starting a new one.
            CleanupConnection_NoLock();

            SetState(ConnectionState.Connecting);
            Console.WriteLine("[Gemini] Connecting to Gemini Live API...");

            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();
            _setupCompleteTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            var uri = new Uri(string.Format(WS_URL_TEMPLATE, _apiKey));

            using var connectCts = new CancellationTokenSource(CONNECT_TIMEOUT_MS);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, connectCts.Token);

            await _webSocket.ConnectAsync(uri, linkedCts.Token).ConfigureAwait(false);
            Console.WriteLine("[Gemini] WebSocket connected");

            // Start receive loop before setup so setupComplete can be observed.
            var ws = _webSocket;
            var token = _cts.Token;
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(ws, token));

            // Send setup message with agent's system instruction
            await SendSetupMessageAsync(agent).ConfigureAwait(false);
            Console.WriteLine("[Gemini] Setup message sent");

            await _setupCompleteTcs.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            SetState(ConnectionState.Connected);
            Console.WriteLine("[Gemini] Connection established, receive loop starting");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Gemini] Connection timeout");
            SetState(ConnectionState.Error);
            ErrorOccurred?.Invoke(this, "Connection timeout. Please check your internet connection.");
            CleanupConnection_NoLock();
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"[Gemini] WebSocket error: {ex.Message}");
            SetState(ConnectionState.Error);
            ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}");
            CleanupConnection_NoLock();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gemini] Unexpected error: {ex}");
            SetState(ConnectionState.Error);
            ErrorOccurred?.Invoke(this, $"Unexpected error: {ex.Message}");
            CleanupConnection_NoLock();
        }
        finally
        {
            _connectLock.Release();
        }
    }
    
    private void SetState(ConnectionState state)
    {
        lock (_stateLock)
        {
            if (_state == state) return;
            _state = state;
        }
        // Fire outside lock to prevent deadlocks with subscribers
        Console.WriteLine($"[Gemini] State -> {state}");
        ConnectionStateChanged?.Invoke(this, state);
    }
    
    private async Task SendSetupMessageAsync(Agent agent)
    {
        var instructions = _instructionsOverride ?? agent.ComposedPersonality;
        var json = GeminiLiveProtocol.BuildSetupMessageJson(
            instructions, _voice,
            resumptionHandle: _lastResumptionHandle);
        await SendRawJsonAsync(json).ConfigureAwait(false);
    }

    private async Task SendRawJsonAsync(string json)
    {
        var ws = _webSocket;
        var cts = _cts;
        if (ws?.State != WebSocketState.Open || cts == null)
            return;

        var bytes = Encoding.UTF8.GetBytes(json);
        
        // Log first message (setup) for debugging
        if (json.Contains("setup"))
        {
            Console.WriteLine($"[Gemini] Sending setup: {json}");
        }

        try
        {
            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cts.Token
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to send message: {ex.Message}");
        }
    }
    
    private long _audioSendCount;
    
    public async Task SendAudioAsync(byte[] audioData)
    {
        if (!IsConnected) return;

        var json = GeminiLiveProtocol.BuildAudioMessageJson(audioData);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        try
        {
            var ws = _webSocket;
            var cts = _cts;
            if (ws?.State != WebSocketState.Open || cts == null) return;

            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cts.Token
            ).ConfigureAwait(false);
            
            var count = Interlocked.Increment(ref _audioSendCount);
            if (count <= 5 || count % 100 == 0)
            {
                Console.WriteLine($"[Gemini] Sent audio chunk #{count}: {audioData.Length} bytes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gemini] Failed to send audio: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Failed to send audio: {ex.Message}");
        }
    }
    
    public async Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg")
    {
        if (!IsConnected) return;

        var json = GeminiLiveProtocol.BuildImageMessageJson(imageData, mimeType);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        try
        {
            var ws = _webSocket;
            var cts = _cts;
            if (ws?.State != WebSocketState.Open || cts == null) return;

            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cts.Token
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to send image: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends user text to the conversation via realtime_input.
    /// </summary>
    public async Task SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!IsConnected)
        {
            const string message = "Cannot send text: Gemini provider is not connected.";
            ErrorOccurred?.Invoke(this, message);
            throw new InvalidOperationException(message);
        }

        var json = GeminiLiveProtocol.BuildTextMessageJson(text);
        var bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            var ws = _webSocket;
            var cts = _cts;
            if (ws?.State != WebSocketState.Open || cts == null) return;

            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cts.Token
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to send text: {ex.Message}");
        }
    }

    private long _messageReceiveCount;
    
    private async Task ReceiveLoopAsync(ClientWebSocket? ws, CancellationToken token)
    {
        if (ws is null) return;
        
        Console.WriteLine("[Gemini] Receive loop started");

        var buffer = new byte[RECEIVE_BUFFER_SIZE];
        var messageBuffer = new MemoryStream();
        
        try
        {
            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    token
                ).ConfigureAwait(false);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"[Gemini] Server closed connection. CloseStatus: {ws.CloseStatus}, CloseDescription: {ws.CloseStatusDescription}");
                    if (!_disposed)
                    {
                        var reason = ws.CloseStatusDescription ?? "Gemini Live closed the connection during setup.";
                        SetState(ConnectionState.Error);
                        ErrorOccurred?.Invoke(this, reason);
                        _setupCompleteTcs?.TrySetException(new InvalidOperationException(reason));
                    }
                    break;
                }
                
                // Gemini Live API sends JSON responses as Binary frames (not Text).
                // Handle both frame types identically since the payload is always UTF-8 JSON.
                if (result.MessageType == WebSocketMessageType.Text
                    || result.MessageType == WebSocketMessageType.Binary)
                {
                    // Append to message buffer
                    messageBuffer.Write(buffer, 0, result.Count);

                    // Only process when we have the complete message
                    if (result.EndOfMessage)
                    {
                        var count = Interlocked.Increment(ref _messageReceiveCount);
                        var json = Encoding.UTF8.GetString(
                            messageBuffer.GetBuffer(),
                            0,
                            (int)messageBuffer.Length
                        );

                        // Log first few messages or every 10th
                        if (count <= 3 || count % 10 == 0)
                        {
                            Console.WriteLine($"[Gemini] Received message #{count}: {json.Length} chars");
                        }

                        // Reset buffer for next message
                        messageBuffer.SetLength(0);

                        ProcessMessage(json);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Gemini] Receive loop cancelled (normal shutdown)");
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"[Gemini] WebSocket exception in receive loop: {ex.Message}");
            // Connection lost - attempt reconnect with backoff.
            if (!_disposed)
            {
                _ = ReconnectWithBackoffAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gemini] Receive loop error: {ex}");
            SetState(ConnectionState.Error);
            ErrorOccurred?.Invoke(this, $"Receive error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("[Gemini] Receive loop ended");
            messageBuffer.Dispose();
        }
    }
    
    private async Task ReconnectWithBackoffAsync()
    {
        if (_disposed) return;
        if (_currentAgent is null)
        {
            SetState(ConnectionState.Disconnected);
            return;
        }

        SetState(ConnectionState.Reconnecting);

        // Tear down any existing connection before retrying.
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            CleanupConnection_NoLock();
        }
        finally
        {
            _connectLock.Release();
        }

        var delaysMs = new[] { 1000, 2000, 5000, 10_000, 30_000 };
        for (int attempt = 0; attempt < delaysMs.Length; attempt++)
        {
            if (_disposed) return;

            try
            {
                await Task.Delay(delaysMs[attempt]).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            if (_disposed) return;

            try
            {
                await ConnectAsync(_currentAgent).ConfigureAwait(false);
                if (IsConnected) return;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Reconnection attempt {attempt + 1} failed: {ex.Message}");
            }
        }

        SetState(ConnectionState.Error);
        ErrorOccurred?.Invoke(this, "Failed to reconnect after multiple attempts.");
    }
    
    private void ProcessMessage(string json)
    {
        try
        {
            var kind = GeminiLiveProtocol.ClassifyServerMessage(json);
            if (kind == GeminiServerMessageKind.SetupComplete)
            {
                Console.WriteLine("[Gemini] Setup complete - ready to receive audio");
                _setupCompleteTcs?.TrySetResult();
                return;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (kind == GeminiServerMessageKind.SessionResumptionUpdate)
            {
                if (root.TryGetProperty("sessionResumptionUpdate", out var update) &&
                    update.TryGetProperty("newHandle", out var handleEl))
                {
                    _lastResumptionHandle = handleEl.GetString();
                    Console.WriteLine("[Gemini] Session resumption handle updated");
                }
                return;
            }

            if (kind == GeminiServerMessageKind.GoAway)
            {
                Console.WriteLine($"[Gemini] Received GoAway — server will disconnect soon (handle={(_lastResumptionHandle != null ? "available" : "NONE")})");
                if (!_disposed)
                {
                    _ = ReconnectWithBackoffAsync();
                }
                return;
            }

            // Check for server content (audio/interruption)
            if (root.TryGetProperty("serverContent", out var serverContent))
            {
                // Check for interruption first
                if (serverContent.TryGetProperty("interrupted", out var interrupted) &&
                    interrupted.GetBoolean())
                {
                    Interrupted?.Invoke(this, EventArgs.Empty);
                    return;
                }
                
                // Parse input transcription (user's speech-to-text)
                if (serverContent.TryGetProperty("inputTranscription", out var inputTranscription))
                {
                    if (inputTranscription.TryGetProperty("text", out var transcriptText))
                    {
                        var transcript = transcriptText.GetString();
                        if (!string.IsNullOrEmpty(transcript))
                        {
                            Console.WriteLine($"[Gemini] User said: {transcript}");
                            InputTranscriptionReceived?.Invoke(this, transcript);
                        }
                    }
                }

                // Check for turn complete
                if (serverContent.TryGetProperty("turnComplete", out var turnComplete) &&
                    turnComplete.GetBoolean())
                {
                    System.Diagnostics.Debug.WriteLine("[Gemini] Turn complete");
                    return;
                }
                
                // Extract audio/text data from modelTurn
                if (serverContent.TryGetProperty("modelTurn", out var modelTurn) &&
                    modelTurn.TryGetProperty("parts", out var parts))
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        ProcessPart(part);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            // Invalid JSON - log but don't crash
            System.Diagnostics.Debug.WriteLine($"[Gemini] JSON parse error: {ex.Message}");
        }
    }
    
    private void ProcessPart(JsonElement part)
    {
        // Check for inline data (audio/images)
        if (part.TryGetProperty("inlineData", out var inlineData))
        {
            if (!inlineData.TryGetProperty("mimeType", out var mimeTypeElement) ||
                !inlineData.TryGetProperty("data", out var dataElement))
                return;
            
            var mimeType = mimeTypeElement.GetString();
            var base64Data = dataElement.GetString();
            
            if (string.IsNullOrEmpty(mimeType) || string.IsNullOrEmpty(base64Data))
                return;
            
            // Audio response
            if (mimeType.StartsWith("audio/"))
            {
                try
                {
                    var pcmData = Convert.FromBase64String(base64Data);
                    Console.WriteLine($"[Gemini] Received audio response: {pcmData.Length} bytes");
                    AudioReceived?.Invoke(this, pcmData);
                }
                catch (FormatException)
                {
                    Console.WriteLine("[Gemini] Invalid base64 audio data");
                }
            }
        }
        
        // Check for text content
        if (part.TryGetProperty("text", out var textElement))
        {
            var text = textElement.GetString();
            if (!string.IsNullOrEmpty(text))
            {
                TextReceived?.Invoke(this, text);
            }
        }
    }
    
    /// <summary>
    /// Reconnects with updated system instructions. Uses session resumption handle
    /// for context continuity. This is the only way to update instructions on
    /// Gemini Live (mid-session updates are not supported by the API).
    /// </summary>
    public async Task ReconnectWithInstructionsAsync(string instructions)
    {
        if (_disposed) return;
        _instructionsOverride = instructions;
        await DisconnectAsync().ConfigureAwait(false);
        if (_currentAgent != null)
            await ConnectAsync(_currentAgent).ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;

            var ws = _webSocket;
            var cts = _cts;

            _webSocket = null;
            _cts = null;
            _receiveLoopTask = null;

            try { cts?.Cancel(); } catch { /* best effort */ }

            if (ws?.State == WebSocketState.Open || ws?.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "User disconnected",
                        CancellationToken.None
                    ).ConfigureAwait(false);
                }
                catch
                {
                    // best effort
                }
            }

            try { ws?.Abort(); } catch { /* best effort */ }
            ws?.Dispose();
            cts?.Dispose();

            SetState(ConnectionState.Disconnected);
        }
        finally
        {
            _connectLock.Release();
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;

        CleanupConnection_NoLock();
        _connectLock.Dispose();
    }

    private void CleanupConnection_NoLock()
    {
        var ws = _webSocket;
        var cts = _cts;
        var setupCompleteTcs = _setupCompleteTcs;

        _webSocket = null;
        _cts = null;
        _receiveLoopTask = null;
        _setupCompleteTcs = null;
        _lastResumptionHandle = null; // Clear stale handle — expired handles cause silent reconnect failures
        _instructionsOverride = null; // Reset to agent's default personality on next connect

        setupCompleteTcs?.TrySetCanceled();
        try { cts?.Cancel(); } catch { /* best effort */ }
        try { ws?.Abort(); } catch { /* best effort */ }
        try { ws?.Dispose(); } catch { /* best effort */ }
        try { cts?.Dispose(); } catch { /* best effort */ }
    }
}

internal enum GeminiServerMessageKind
{
    Unknown,
    SetupComplete,
    ServerContent,
    GoAway,
    SessionResumptionUpdate
}

internal static class GeminiLiveProtocol
{
    private const string Model = "models/gemini-3.1-flash-live-preview";

    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static string BuildSetupMessageJson(string systemInstruction, string voice,
        string? resumptionHandle = null)
    {
        var payload = new
        {
            setup = new
            {
                model = Model,
                generationConfig = new
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new
                    {
                        voiceConfig = new
                        {
                            prebuiltVoiceConfig = new
                            {
                                voiceName = voice
                            }
                        }
                    }
                },
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = systemInstruction }
                    }
                },
                inputAudioTranscription = new { },
                outputAudioTranscription = new { },
                sessionResumption = resumptionHandle != null
                    ? (object)new { handle = resumptionHandle }
                    : new { },
                contextWindowCompression = new { slidingWindow = new { } },
                realtimeInputConfig = new
                {
                    automaticActivityDetection = new
                    {
                        disabled = false
                    },
                    activityHandling = "START_OF_ACTIVITY_INTERRUPTS"
                }
            }
        };

        return JsonSerializer.Serialize(payload, CamelCaseJson);
    }

    internal static string BuildAudioMessageJson(byte[] audioData)
    {
        var payload = new
        {
            realtimeInput = new
            {
                audio = new
                {
                    mimeType = "audio/pcm;rate=16000",
                    data = Convert.ToBase64String(audioData)
                }
            }
        };

        return JsonSerializer.Serialize(payload, CamelCaseJson);
    }

    internal static string BuildImageMessageJson(byte[] imageData, string mimeType)
    {
        var payload = new
        {
            realtimeInput = new
            {
                video = new
                {
                    mimeType,
                    data = Convert.ToBase64String(imageData)
                }
            }
        };

        return JsonSerializer.Serialize(payload, CamelCaseJson);
    }

    internal static string BuildTextMessageJson(string text)
    {
        var payload = new
        {
            realtimeInput = new
            {
                text = text.Trim()
            }
        };

        return JsonSerializer.Serialize(payload, CamelCaseJson);
    }

    internal static GeminiServerMessageKind ClassifyServerMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("setupComplete", out _))
            return GeminiServerMessageKind.SetupComplete;
        if (root.TryGetProperty("serverContent", out _))
            return GeminiServerMessageKind.ServerContent;
        if (root.TryGetProperty("goAway", out _))
            return GeminiServerMessageKind.GoAway;
        if (root.TryGetProperty("sessionResumptionUpdate", out _))
            return GeminiServerMessageKind.SessionResumptionUpdate;

        return GeminiServerMessageKind.Unknown;
    }
}
