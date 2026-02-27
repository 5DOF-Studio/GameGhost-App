using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// WebSocket client for OpenAI Realtime API real-time audio conversation.
/// </summary>
/// <remarks>
/// <para>
/// This service implements speech-to-speech conversation using OpenAI's Realtime API.
/// Unlike Gemini, OpenAI Realtime currently does not support image/video input.
/// </para>
/// <para>
/// <b>Audio Format:</b><br/>
/// - Input: 24kHz, 16-bit, mono PCM (must resample from app's 16kHz)<br/>
/// - Output: 24kHz, 16-bit, mono PCM (matches app's output format)
/// </para>
/// <para>
/// <b>Authentication:</b> Via Authorization header (Bearer token), not URL parameter.
/// </para>
/// </remarks>
public sealed class OpenAIRealtimeService : IDisposable
{
    private const string WS_URL = "wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-12-17";
    private const string VOICE = "alloy"; // Options: alloy, echo, shimmer, ash, ballad, coral, sage, verse
    private const int CONNECT_TIMEOUT_MS = 30_000;
    private const int RECEIVE_BUFFER_SIZE = 64 * 1024; // 64KB
    
    private readonly string _apiKey;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoopTask;
    private Agent? _currentAgent;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
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
    
    public OpenAIRealtimeService(IConfiguration configuration)
    {
        _apiKey = configuration["OPENAI_APIKEY"]
            ?? configuration["OPENAI_API_KEY"]
            ?? configuration["OpenAiApiKey"]
            ?? configuration["APIKEY"]        // from OPENAI_APIKEY prefix mapping
            ?? configuration["API_KEY"]       // from OPENAI_API_KEY prefix mapping
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
                ErrorOccurred?.Invoke(this, "OpenAI API key not configured. Set OPENAI_APIKEY environment variable.");
                return;
            }

            _currentAgent = agent;

            // Ensure any previous connection attempt is torn down before starting a new one.
            CleanupConnection_NoLock();

            SetState(ConnectionState.Connecting);
            Console.WriteLine("[OpenAI] Connecting to OpenAI Realtime API...");

            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();
            
            // OpenAI uses Authorization header for authentication
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            _webSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

            var uri = new Uri(WS_URL);

            using var connectCts = new CancellationTokenSource(CONNECT_TIMEOUT_MS);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, connectCts.Token);

            await _webSocket.ConnectAsync(uri, linkedCts.Token).ConfigureAwait(false);
            Console.WriteLine("[OpenAI] WebSocket connected");

            // Start receive loop BEFORE sending session.update so we can receive session.created
            var ws = _webSocket;
            var token = _cts.Token;
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(ws, token));

            // Send session.update message with agent's system instruction
            await SendSessionUpdateAsync(agent).ConfigureAwait(false);
            Console.WriteLine("[OpenAI] Session update sent");

            SetState(ConnectionState.Connected);
            Console.WriteLine("[OpenAI] Connection established");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[OpenAI] Connection timeout");
            SetState(ConnectionState.Error);
            ErrorOccurred?.Invoke(this, "Connection timeout. Please check your internet connection.");
            CleanupConnection_NoLock();
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"[OpenAI] WebSocket error: {ex.Message}");
            SetState(ConnectionState.Error);
            ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}");
            CleanupConnection_NoLock();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenAI] Unexpected error: {ex}");
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
    
    private async Task SendSessionUpdateAsync(Agent agent)
    {
        // OpenAI Realtime API session.update message format
        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                modalities = new[] { "text", "audio" },
                instructions = agent.SystemInstruction,
                voice = VOICE,
                input_audio_format = "pcm16",
                output_audio_format = "pcm16",
                input_audio_transcription = new
                {
                    model = "whisper-1"
                },
                turn_detection = new
                {
                    type = "server_vad",
                    threshold = 0.5,
                    prefix_padding_ms = 300,
                    silence_duration_ms = 500
                }
            }
        };
        
        await SendJsonAsync(sessionUpdate);
    }
    
    private async Task SendJsonAsync<T>(T payload)
    {
        var ws = _webSocket;
        var cts = _cts;
        if (ws?.State != WebSocketState.Open || cts == null)
            return;

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        // Log session messages for debugging
        if (json.Contains("session"))
        {
            Console.WriteLine($"[OpenAI] Sending: {json}");
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
    
    /// <summary>
    /// Sends audio data to OpenAI Realtime API.
    /// </summary>
    /// <remarks>
    /// <b>IMPORTANT:</b> Audio MUST be 24kHz, 16-bit, mono PCM.
    /// The caller (OpenAIConversationProvider) is responsible for resampling from 16kHz.
    /// </remarks>
    public async Task SendAudioAsync(byte[] audioData)
    {
        if (!IsConnected) return;
        
        var base64 = Convert.ToBase64String(audioData);
        
        // OpenAI uses input_audio_buffer.append for streaming audio
        var message = new
        {
            type = "input_audio_buffer.append",
            audio = base64
        };
        
        var json = JsonSerializer.Serialize(message, JsonOptions);
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
                Console.WriteLine($"[OpenAI] Sent audio chunk #{count}: {audioData.Length} bytes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenAI] Failed to send audio: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Failed to send audio: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Commits the current audio buffer and triggers a response.
    /// Call this when the user stops speaking (if not using server VAD).
    /// </summary>
    public async Task CommitAudioBufferAsync()
    {
        if (!IsConnected) return;
        
        var message = new { type = "input_audio_buffer.commit" };
        await SendJsonAsync(message);
    }
    
    /// <summary>
    /// Cancels the current AI response (interruption).
    /// </summary>
    public async Task CancelResponseAsync()
    {
        if (!IsConnected) return;
        
        var message = new { type = "response.cancel" };
        await SendJsonAsync(message);
        Console.WriteLine("[OpenAI] Response cancelled");
    }

    /// <summary>
    /// Sends user text to the conversation via conversation.item.create.
    /// </summary>
    public async Task SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(text)) return;

        var message = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "user",
                status = "completed",
                content = new[] { new { type = "input_text", text = text.Trim() } }
            }
        };

        var ws = _webSocket;
        var cts = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(_cts!.Token, cancellationToken)
            : null;
        var token = cts?.Token ?? _cts?.Token ?? cancellationToken;

        try
        {
            if (ws?.State != WebSocketState.Open) return;
            var json = JsonSerializer.Serialize(message, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                token
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to send text: {ex.Message}");
        }
        finally
        {
            cts?.Dispose();
        }
    }

    private long _messageReceiveCount;
    
    private async Task ReceiveLoopAsync(ClientWebSocket? ws, CancellationToken token)
    {
        if (ws is null) return;
        
        Console.WriteLine("[OpenAI] Receive loop started");

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
                    Console.WriteLine($"[OpenAI] Server closed connection. CloseStatus: {ws.CloseStatus}, CloseDescription: {ws.CloseStatusDescription}");
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
                        if (count <= 5 || count % 10 == 0)
                        {
                            // Truncate audio data for logging
                            var logJson = json.Length > 500 ? json[..500] + "..." : json;
                            Console.WriteLine($"[OpenAI] Received message #{count}: {logJson}");
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
            Console.WriteLine("[OpenAI] Receive loop cancelled (normal shutdown)");
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"[OpenAI] WebSocket exception in receive loop: {ex.Message}");
            // Connection lost - attempt reconnect with backoff.
            if (!_disposed)
            {
                _ = ReconnectWithBackoffAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenAI] Receive loop error: {ex}");
            SetState(ConnectionState.Error);
            ErrorOccurred?.Invoke(this, $"Receive error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("[OpenAI] Receive loop ended");
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
            
            if (!root.TryGetProperty("type", out var typeElement))
                return;
                
            var type = typeElement.GetString();
            
            switch (type)
            {
                case "session.created":
                    Console.WriteLine("[OpenAI] Session created - ready for conversation");
                    break;
                    
                case "session.updated":
                    Console.WriteLine("[OpenAI] Session updated");
                    break;
                    
                case "input_audio_buffer.speech_started":
                    Console.WriteLine("[OpenAI] User speech started (VAD detected)");
                    break;
                    
                case "input_audio_buffer.speech_stopped":
                    Console.WriteLine("[OpenAI] User speech stopped (VAD detected)");
                    break;
                    
                case "input_audio_buffer.committed":
                    Console.WriteLine("[OpenAI] Audio buffer committed");
                    break;
                    
                case "response.created":
                    Console.WriteLine("[OpenAI] Response started");
                    break;
                    
                case "response.audio.delta":
                    ProcessAudioDelta(root);
                    break;
                    
                case "response.audio.done":
                    Console.WriteLine("[OpenAI] Audio response complete");
                    break;
                    
                case "response.audio_transcript.delta":
                    ProcessTranscriptDelta(root);
                    break;
                    
                case "response.audio_transcript.done":
                    ProcessTranscriptDone(root);
                    break;
                    
                case "response.text.delta":
                    ProcessTextDelta(root);
                    break;
                    
                case "response.text.done":
                    ProcessTextDone(root);
                    break;
                    
                case "response.done":
                    Console.WriteLine("[OpenAI] Response complete");
                    break;
                    
                case "conversation.item.input_audio_transcription.completed":
                    ProcessInputTranscription(root);
                    break;
                    
                case "error":
                    ProcessError(root);
                    break;
                    
                case "rate_limits.updated":
                    // Rate limit info - log but don't process
                    break;
                    
                default:
                    System.Diagnostics.Debug.WriteLine($"[OpenAI] Unhandled message type: {type}");
                    break;
            }
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpenAI] JSON parse error: {ex.Message}");
        }
    }
    
    private void ProcessAudioDelta(JsonElement root)
    {
        if (!root.TryGetProperty("delta", out var deltaElement))
            return;
            
        var base64Audio = deltaElement.GetString();
        if (string.IsNullOrEmpty(base64Audio))
            return;
            
        try
        {
            var pcmData = Convert.FromBase64String(base64Audio);
            AudioReceived?.Invoke(this, pcmData);
        }
        catch (FormatException)
        {
            Console.WriteLine("[OpenAI] Invalid base64 audio data");
        }
    }
    
    private void ProcessTranscriptDelta(JsonElement root)
    {
        if (!root.TryGetProperty("delta", out var deltaElement))
            return;
            
        var text = deltaElement.GetString();
        if (!string.IsNullOrEmpty(text))
        {
            // Could accumulate for streaming display, but for now just log
            System.Diagnostics.Debug.WriteLine($"[OpenAI] Transcript delta: {text}");
        }
    }
    
    private void ProcessTranscriptDone(JsonElement root)
    {
        if (!root.TryGetProperty("transcript", out var transcriptElement))
            return;
            
        var transcript = transcriptElement.GetString();
        if (!string.IsNullOrEmpty(transcript))
        {
            Console.WriteLine($"[OpenAI] AI said: {transcript}");
            TextReceived?.Invoke(this, transcript);
        }
    }
    
    private void ProcessTextDelta(JsonElement root)
    {
        if (!root.TryGetProperty("delta", out var deltaElement))
            return;
            
        var text = deltaElement.GetString();
        if (!string.IsNullOrEmpty(text))
        {
            System.Diagnostics.Debug.WriteLine($"[OpenAI] Text delta: {text}");
        }
    }
    
    private void ProcessTextDone(JsonElement root)
    {
        if (!root.TryGetProperty("text", out var textElement))
            return;
            
        var text = textElement.GetString();
        if (!string.IsNullOrEmpty(text))
        {
            TextReceived?.Invoke(this, text);
        }
    }
    
    private void ProcessInputTranscription(JsonElement root)
    {
        if (!root.TryGetProperty("transcript", out var transcriptElement))
            return;
            
        var transcript = transcriptElement.GetString();
        if (!string.IsNullOrEmpty(transcript))
        {
            Console.WriteLine($"[OpenAI] User said: {transcript}");
        }
    }
    
    private void ProcessError(JsonElement root)
    {
        var errorMessage = "Unknown error";
        
        if (root.TryGetProperty("error", out var errorElement))
        {
            if (errorElement.TryGetProperty("message", out var messageElement))
            {
                errorMessage = messageElement.GetString() ?? errorMessage;
            }
        }
        
        Console.WriteLine($"[OpenAI] Error: {errorMessage}");
        ErrorOccurred?.Invoke(this, errorMessage);
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

