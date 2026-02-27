using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// WebSocket client for Gemini Live API real-time audio/visual interaction.
/// </summary>
public sealed class GeminiLiveService : IGeminiService
{
    private const string WS_URL_TEMPLATE = 
        "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={0}";
    private const string MODEL = "models/gemini-2.0-flash-exp";
    private const string VOICE = "Fenrir";
    private const int CONNECT_TIMEOUT_MS = 30_000;
    private const int RECEIVE_BUFFER_SIZE = 64 * 1024; // 64KB
    
    private readonly string _apiKey;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoopTask;
    private Agent? _currentAgent;
    private bool _disposed;

    private static readonly JsonSerializerOptions SnakeCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public bool IsConnected => State == ConnectionState.Connected;
    
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TextReceived;
    public event EventHandler? Interrupted;
    public event EventHandler<string>? ErrorOccurred;
    
    public GeminiLiveService(IConfiguration configuration)
    {
        _apiKey = configuration["GeminiApiKey"]
            ?? configuration["APIKEY"]          // Environment variable: GEMINI_APIKEY (via prefix mapping)
            ?? configuration["API_KEY"]         // Environment variable: GEMINI_API_KEY (via prefix mapping)
            ?? configuration["GEMINI_APIKEY"]   // Direct env var (unprefixed load)
            ?? configuration["GEMINI_API_KEY"]  // Direct env var (unprefixed load)
            ?? string.Empty;
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

            var uri = new Uri(string.Format(WS_URL_TEMPLATE, _apiKey));

            using var connectCts = new CancellationTokenSource(CONNECT_TIMEOUT_MS);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, connectCts.Token);

            await _webSocket.ConnectAsync(uri, linkedCts.Token).ConfigureAwait(false);
            Console.WriteLine("[Gemini] WebSocket connected");

            // Send setup message with agent's system instruction
            await SendSetupMessageAsync(agent).ConfigureAwait(false);
            Console.WriteLine("[Gemini] Setup message sent");

            SetState(ConnectionState.Connected);
            Console.WriteLine("[Gemini] Connection established, receive loop starting");

            // Start receive loop (fire and forget) using stable references.
            var ws = _webSocket;
            var token = _cts.Token;
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(ws, token));
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
        if (State != state)
        {
            State = state;
            ConnectionStateChanged?.Invoke(this, state);
        }
    }
    
    private async Task SendSetupMessageAsync(Agent agent)
    {
        var setup = new
        {
            setup = new
            {
                model = MODEL,
                generation_config = new
                {
                    response_modalities = new[] { "AUDIO" },
                    speech_config = new
                    {
                        voice_config = new
                        {
                            prebuilt_voice_config = new
                            {
                                voice_name = VOICE
                            }
                        }
                    }
                },
                system_instruction = new
                {
                    parts = new[]
                    {
                        new { text = agent.SystemInstruction }
                    }
                }
            }
        };
        
        await SendJsonAsync(setup);
    }
    
    private async Task SendJsonAsync<T>(T payload)
    {
        var ws = _webSocket;
        var cts = _cts;
        if (ws?.State != WebSocketState.Open || cts == null)
            return;

        var json = JsonSerializer.Serialize(payload, SnakeCaseJson);
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
        
        var base64 = Convert.ToBase64String(audioData);
        
        var message = new
        {
            realtime_input = new
            {
                media_chunks = new[]
                {
                    new
                    {
                        mime_type = "audio/pcm;rate=16000",
                        data = base64
                    }
                }
            }
        };
        
        var json = JsonSerializer.Serialize(message, SnakeCaseJson);
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
        
        var base64 = Convert.ToBase64String(imageData);
        
        var message = new
        {
            realtime_input = new
            {
                media_chunks = new[]
                {
                    new
                    {
                        mime_type = mimeType,
                        data = base64
                    }
                }
            }
        };
        
        var json = JsonSerializer.Serialize(message, SnakeCaseJson);
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
        if (!IsConnected || string.IsNullOrWhiteSpace(text)) return;

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text.Trim()));
        var message = new
        {
            realtime_input = new
            {
                media_chunks = new[]
                {
                    new
                    {
                        mime_type = "text/plain",
                        data = base64
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(message, SnakeCaseJson);
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
                    break;
                }
                
                if (result.MessageType == WebSocketMessageType.Text)
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
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Check for setup confirmation (optional logging)
            if (root.TryGetProperty("setupComplete", out _))
            {
                Console.WriteLine("[Gemini] Setup complete - ready to receive audio");
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

        _webSocket = null;
        _cts = null;
        _receiveLoopTask = null;

        try { cts?.Cancel(); } catch { /* best effort */ }
        try { ws?.Abort(); } catch { /* best effort */ }
        try { ws?.Dispose(); } catch { /* best effort */ }
        try { cts?.Dispose(); } catch { /* best effort */ }
    }
}

