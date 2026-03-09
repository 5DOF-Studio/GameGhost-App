# Phase 3: Gemini API Integration Implementation Plan

**Version:** 1.3.0  
**Date:** December 14, 2025  
**Scope:** WebSocket client for Gemini Live API, audio/image transmission, and response handling  
**Status:** ✅ **Verified on macOS — Blocked on API Quota (External)**  
**Target Completion:** Week 3-4

---

## Revision History

- **1.3.0 (Dec 14, 2025):** On-device testing completed on macOS. Fixed model name (`gemini-2.0-flash-exp`), verified WebSocket connection works, mic capture verified. Blocked on external quota issue. Added deferred improvement for user-facing error messages.
- **1.2.1 (Dec 14, 2025):** Documentation review and appraisal. Confirmed implementation quality exceeds plan requirements. All tasks marked complete. Ready for on-device testing.
- **1.2.0 (Dec 14, 2025):** Implementation completed in codebase. Added reconnect-with-backoff + safer teardown in `GeminiLiveService`. Updated DI/config wiring and MainViewModel integration. Docs marked as implemented; testing checklist remains pending.
- **1.1.0 (Dec 13, 2025):** Pre-implementation review. Aligned plan with Phase 2 completed audio contract (callback-based), current `IWindowCaptureService` (event-based), existing `Agent.SystemInstruction` property, and `ConnectionState` enum. Added configuration setup task. Fixed WebSocket message assembly. Clarified MainViewModel integration patterns.
- **1.0.0 (Dec 13, 2025):** Initial plan.

---

## Overview

This document outlines the implementation plan for Phase 3: Integration, which connects the audio and window capture systems to the Gemini Live API via WebSocket. This phase enables end-to-end voice interaction and visual analysis.

**Key Components:**
1. **WebSocket Client** - Connection to Gemini Live API
2. **Connection Management** - State handling, reconnection logic
3. **Audio Transmission** - Send user microphone audio (PCM) to API
4. **Image Transmission** - Send window screenshots (JPEG) to API
5. **Response Handling** - Receive and process AI audio/text responses
6. **Interruption Handling** - Handle user interruptions during AI responses

**Reference Specifications:**
- `GAIMER_DESKTOP_APP_SPEC.md` Section 6: Network Layer
- `gaimer_design_spec.md` Section 2: Agent System (system prompts)

---

## Objectives

- Replace `MockGeminiService` with real WebSocket-based implementation
- Establish stable connection to Gemini Live API
- Transmit audio and image data in correct format (base64, MIME types)
- Handle AI responses (audio playback, text display)
- Implement interruption support for real-time conversation
- Support agent-specific system prompts (via `Agent.SystemInstruction`)
- Handle errors and reconnection gracefully

---

## Current State

### Existing Interface (`IGeminiService.cs`)

```csharp
public interface IGeminiService
{
    event EventHandler<ConnectionState>? ConnectionStateChanged;
    event EventHandler<byte[]>? AudioReceived;
    event EventHandler<string>? TextReceived;
    event EventHandler? Interrupted;

    Task ConnectAsync(Agent agent);
    Task DisconnectAsync();
    Task SendAudioAsync(byte[] audioData);
    Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg");
    ConnectionState State { get; }
}
```

### Existing ConnectionState Enum

```csharp
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}
```

### Current Mock Implementation

- `MockGeminiService` simulates connection and responses
- Generates random text responses every 5-15 seconds
- No real WebSocket communication

### Existing Agent Model (has SystemInstruction)

```csharp
public class Agent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    // ... other properties
    public required string SystemInstruction { get; init; }  // ✅ Already exists!
}
```

### Phase 2 Completed Audio Contract

```csharp
// Callback-based recording (NOT event-based)
Task StartRecordingAsync(Action<byte[]> onAudioCaptured);
```

### Current IWindowCaptureService

```csharp
// Event-based capture (NOT callback-based)
event EventHandler<byte[]>? FrameCaptured;
Task StartCaptureAsync(CaptureTarget target);
```

### Required Interface Updates

Add to `IGeminiService`:

```csharp
public interface IGeminiService : IDisposable  // Add IDisposable
{
    // ... existing members ...
    
    event EventHandler<string>? ErrorOccurred;  // Add for error handling
    bool IsConnected { get; }                   // Add convenience property
}
```

Add to `ConnectionState` enum:

```csharp
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,  // Add for reconnection logic
    Error
}
```

**Decision:** Update interface with `ErrorOccurred`, `IsConnected`, `IDisposable`, and add `Reconnecting` state.

---

## Prerequisites

### Phase 2 Completion

- ✅ Real audio capture working (`IAudioService` - callback-based)
- ✅ Real audio playback working  
- ✅ Volume monitoring functional (`VolumeChanged` event)

### Phase 1 Status

- ⏳ Window capture uses mock service (platform implementation Phase 1 blocker)
- ✅ `IWindowCaptureService.FrameCaptured` event exists and is wired in MainViewModel

### API Configuration

- Gemini API key configured (user-secrets or environment variable)
- Model: `gemini-2.5-flash-preview-native-audio-dialog`
- Voice: `Fenrir` (configurable per agent)

### Dependencies

- `System.Net.WebSockets.ClientWebSocket` (built-in .NET)
- `System.Text.Json` (built-in .NET)
- `Microsoft.Extensions.Configuration.UserSecrets` (for user-secrets support)
- No additional NuGet packages required beyond standard .NET

---

## Implementation Tasks

### Task 1: API Configuration Setup

**Goal:** Configure Gemini API key access with IConfiguration infrastructure

**Options:**
1. **User Secrets (Recommended for Development)**
   ```bash
   cd src/GaimerDesktop/GaimerDesktop
   dotnet user-secrets init
   dotnet user-secrets set "GeminiApiKey" "your-api-key-here"
   ```

2. **Environment Variable (Recommended for Production/CI)**
   ```bash
   export GEMINI_APIKEY="your-api-key-here"
   # also supported:
   export GEMINI_API_KEY="your-api-key-here"
   ```

3. **appsettings.json (Not Recommended - Security Risk)**
   - Only for local testing, never commit

**Implementation Steps:**

#### 1.1 Add User Secrets Package Reference

**File:** `src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj`

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="8.0.0" />
</ItemGroup>
```

#### 1.2 Update MauiProgram.cs

**File:** `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs`

```csharp
using Microsoft.Extensions.Configuration;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        // Add configuration sources
        var configBuilder = new ConfigurationBuilder();
        
        // Add user secrets (development)
        configBuilder.AddUserSecrets<MauiProgram>(optional: true);
        
        // Add environment variables (production)
        // Looks for GEMINI_APIKEY which maps to "GeminiApiKey"
        configBuilder.AddEnvironmentVariables("GEMINI_");
        
        var configuration = configBuilder.Build();
        builder.Services.AddSingleton<IConfiguration>(configuration);
        
        // ... rest of setup
    }
}
```

#### 1.3 Update config.example.txt

**File:** `config.example.txt` (already exists - update with clear instructions)

```text
# Gaimer Desktop - Configuration

## API Key Setup (Choose ONE method)

### Method 1: User Secrets (Recommended for Development)
cd src/GaimerDesktop/GaimerDesktop
dotnet user-secrets init
dotnet user-secrets set "GeminiApiKey" "YOUR_API_KEY_HERE"

### Method 2: Environment Variable (Recommended for Production)
# macOS/Linux:
export GEMINI_APIKEY="YOUR_API_KEY_HERE"

# Windows PowerShell:
$env:GEMINI_APIKEY="YOUR_API_KEY_HERE"

# Windows CMD:
set GEMINI_APIKEY=YOUR_API_KEY_HERE

## Get Your API Key
1. Go to https://aistudio.google.com/app/apikey
2. Create a new API key
3. Copy and configure using one of the methods above
```

**Acceptance Criteria:**
- API key can be read from user-secrets on both Windows and macOS
- API key can be read from environment variable  
- `IConfiguration` is available via DI for `GeminiLiveService`
- Clear error message if API key not configured

---

### Task 2: Update Interfaces and Enums

**Goal:** Align interfaces with spec requirements and add error handling

#### 2.1 Update ConnectionState Enum

**File:** `src/GaimerDesktop/GaimerDesktop/Models/ConnectionState.cs`

```csharp
namespace GaimerDesktop.Models;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,  // NEW: for auto-reconnection
    Error
}
```

#### 2.2 Update IGeminiService Interface

**File:** `src/GaimerDesktop/GaimerDesktop/Services/IGeminiService.cs`

```csharp
using GaimerDesktop.Models;

namespace GaimerDesktop.Services;

/// <summary>
/// Service for real-time communication with Gemini Live API via WebSocket.
/// </summary>
public interface IGeminiService : IDisposable
{
    /// <summary>
    /// Raised when connection state changes.
    /// </summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;
    
    /// <summary>
    /// Raised when audio data is received from the AI.
    /// PCM format: 24kHz, 16-bit, mono.
    /// </summary>
    event EventHandler<byte[]>? AudioReceived;
    
    /// <summary>
    /// Raised when text/transcript is received from the AI.
    /// </summary>
    event EventHandler<string>? TextReceived;
    
    /// <summary>
    /// Raised when the AI's response is interrupted (user spoke during playback).
    /// </summary>
    event EventHandler? Interrupted;
    
    /// <summary>
    /// Raised when an error occurs. Message contains user-friendly description.
    /// </summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// Connects to Gemini Live API with the specified agent's system instruction.
    /// </summary>
    Task ConnectAsync(Agent agent);
    
    /// <summary>
    /// Disconnects from the API gracefully.
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Sends audio data to the API. Must be 16kHz, 16-bit, mono PCM.
    /// </summary>
    Task SendAudioAsync(byte[] audioData);
    
    /// <summary>
    /// Sends image data to the API.
    /// </summary>
    Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg");
    
    /// <summary>
    /// Current connection state.
    /// </summary>
    ConnectionState State { get; }
    
    /// <summary>
    /// True when State == Connected.
    /// </summary>
    bool IsConnected { get; }
}
```

#### 2.3 Update MockGeminiService

**File:** `src/GaimerDesktop/GaimerDesktop/Services/MockGeminiService.cs`

Add:
- `ErrorOccurred` event declaration
- `IsConnected` property: `public bool IsConnected => State == ConnectionState.Connected;`
- Handle `Reconnecting` state in any switch statements
- Implement `IDisposable.Dispose()` (can be empty for mock)

#### 2.4 Update MainViewModel (if needed)

Check for any switch statements on `ConnectionState` and add `Reconnecting` case:

```csharp
public string ConnectionBadgeText => ConnectionState switch
{
    ConnectionState.Disconnected => "OFFLINE",
    ConnectionState.Connecting => "CONNECTING",
    ConnectionState.Connected => "CONNECTED",
    ConnectionState.Reconnecting => "RECONNECTING",  // NEW
    ConnectionState.Error => "ERROR",
    _ => "UNKNOWN"
};
```

**Acceptance Criteria:**
- `ConnectionState.Reconnecting` added to enum
- `IGeminiService` extends `IDisposable`
- `IGeminiService.ErrorOccurred` event added
- `IGeminiService.IsConnected` property added
- `MockGeminiService` updated and compiles
- `MainViewModel` handles `Reconnecting` state in UI

---

### Task 3: Gemini Live Service Implementation

**Goal:** Implement real WebSocket client for Gemini Live API

**File:** `src/GaimerDesktop/GaimerDesktop/Services/GeminiLiveService.cs`

**Key Components:**

#### 3.1 Class Structure

```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using GaimerDesktop.Models;

namespace GaimerDesktop.Services;

/// <summary>
/// WebSocket client for Gemini Live API real-time audio/visual interaction.
/// </summary>
public sealed class GeminiLiveService : IGeminiService
{
    private const string WS_URL_TEMPLATE = 
        "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={0}";
    private const string MODEL = "models/gemini-2.5-flash-preview-native-audio-dialog";
    private const string VOICE = "Fenrir";
    private const int CONNECT_TIMEOUT_MS = 30_000;
    private const int RECEIVE_BUFFER_SIZE = 64 * 1024; // 64KB
    
    private readonly string _apiKey;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Agent? _currentAgent;
    private bool _disposed;
    
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
            ?? configuration["APIKEY"]  // Environment variable: GEMINI_APIKEY
            ?? string.Empty;
    }
    
    // ... implementation methods follow
}
```

#### 3.2 Connection Management

**WebSocket URL:**
```
wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={API_KEY}
```

**Connection Flow:**
1. Validate API key is configured
2. Create `ClientWebSocket` instance
3. Connect to WebSocket URL with timeout
4. Send setup message (model, voice, agent's system instruction)
5. Start receive loop on background task
6. Update state to `Connected`

**Implementation:**
```csharp
public async Task ConnectAsync(Agent agent)
{
    if (State == ConnectionState.Connected || State == ConnectionState.Connecting)
        return;
    
    if (string.IsNullOrEmpty(_apiKey))
    {
        SetState(ConnectionState.Error);
        ErrorOccurred?.Invoke(this, "API key not configured. See config.example.txt for setup instructions.");
        return;
    }
    
    _currentAgent = agent;
    
    try
    {
        SetState(ConnectionState.Connecting);
        
        _cts = new CancellationTokenSource();
        _webSocket = new ClientWebSocket();
        
        var uri = new Uri(string.Format(WS_URL_TEMPLATE, _apiKey));
        
        using var connectCts = new CancellationTokenSource(CONNECT_TIMEOUT_MS);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, connectCts.Token);
        
        await _webSocket.ConnectAsync(uri, linkedCts.Token);
        
        // Send setup message with agent's system instruction
        await SendSetupMessageAsync(agent);
        
        SetState(ConnectionState.Connected);
        
        // Start receive loop (fire and forget)
        _ = ReceiveLoopAsync();
    }
    catch (OperationCanceledException)
    {
        SetState(ConnectionState.Error);
        ErrorOccurred?.Invoke(this, "Connection timeout. Please check your internet connection.");
    }
    catch (WebSocketException ex)
    {
        SetState(ConnectionState.Error);
        ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}");
    }
    catch (Exception ex)
    {
        SetState(ConnectionState.Error);
        ErrorOccurred?.Invoke(this, $"Unexpected error: {ex.Message}");
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
```

#### 3.3 Setup Message

**Format:** JSON message sent immediately after connection

```json
{
  "setup": {
    "model": "models/gemini-2.5-flash-preview-native-audio-dialog",
    "generation_config": {
      "response_modalities": ["AUDIO"],
      "speech_config": {
        "voice_config": {
          "prebuilt_voice_config": {
            "voice_name": "Fenrir"
          }
        }
      }
    },
    "system_instruction": {
      "parts": [
        {
          "text": "{AGENT.SYSTEMINSTRUCTON}"
        }
      ]
    }
  }
}
```

**Note:** Uses `Agent.SystemInstruction` property which already exists in the model.

**Implementation:**
```csharp
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
    if (_webSocket?.State != WebSocketState.Open || _cts == null)
        return;
    
    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    });
    var bytes = Encoding.UTF8.GetBytes(json);
    
    await _webSocket.SendAsync(
        new ArraySegment<byte>(bytes),
        WebSocketMessageType.Text,
        endOfMessage: true,
        _cts.Token
    );
}
```

#### 3.3 Audio Transmission

**Format:** Base64-encoded PCM data with MIME type

**Message Format:**
```json
{
  "realtime_input": {
    "media_chunks": [
      {
        "mime_type": "audio/pcm;rate=16000",
        "data": "{base64_encoded_pcm}"
      }
    ]
  }
}
```

**Implementation:**
```csharp
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
    
    var json = JsonSerializer.Serialize(message);
    var bytes = Encoding.UTF8.GetBytes(json);
    
    await _webSocket!.SendAsync(
        new ArraySegment<byte>(bytes),
        WebSocketMessageType.Text,
        true,
        _cts!.Token
    );
}
```

**Performance Considerations:**
- Send audio chunks as they arrive (from `IAudioService.AudioCaptured`)
- Don't buffer too much (keep latency low)
- Handle send errors gracefully

#### 3.4 Image Transmission

**Format:** Base64-encoded JPEG with MIME type

**Message Format:**
```json
{
  "realtime_input": {
    "media_chunks": [
      {
        "mime_type": "image/jpeg",
        "data": "{base64_encoded_jpeg}"
      }
    ]
  }
}
```

**Implementation:**
```csharp
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
    
    var json = JsonSerializer.Serialize(message);
    var bytes = Encoding.UTF8.GetBytes(json);
    
    await _webSocket!.SendAsync(
        new ArraySegment<byte>(bytes),
        WebSocketMessageType.Text,
        true,
        _cts!.Token
    );
}
```

**Rate Limiting:**
- **Baseline (default)**: capture frames locally into a short-retention **Visual Reel** at ~1 FPS (cheap), and do **not** continuously transmit images just because the WebSocket is connected.
- **Live / High Attention mode (user toggle, expensive)**: continuously transmit the latest available frame at **2–3 FPS** (or adaptive), targeting end-to-end loop time of **< 2 seconds**.
- **Drop-frame / latest-frame-wins**: never queue frames for inference; if the model is slow, skip older frames and always process the newest.
- **Diff-gating (avoid duplicates)**: compute a lightweight perceptual hash (e.g., **dHash**) on a downscaled analysis image and skip storing/sending near-identical frames; also keep a periodic keepalive frame (e.g., every 5–10s) so the reel has coverage during static scenes.
- **Payload control**: transmit a downscaled “analysis resolution” JPEG (quality tuned for speed/cost). Store higher-quality frames locally only if needed.
- **Connection readiness**: don't send if connection not ready; handle large images gracefully.

**Behavior Policy (agent + UX):**
- **Default**: speak/respond only when spoken to; the agent may still auto-check the latest frame or reel via tools when needed to answer.
- **Live / High Attention**: speak/respond only on detection, using **strict + uncertainty** signaling (alert on explicit matches and on high-confidence “possible” detections above a threshold). Do not produce continuous narration when nothing is detected.
- **Retention**: reel is short-lived; older data may be compressed and queued for deletion, with automatic purge within a bounded time/window (e.g., <= 1 hour) or disk-threshold. Long-term persistence is explicit (user/system instruction) and out of scope for this phase.

#### 3.5 Response Handling

**Receive Loop:**
- Continuously read from WebSocket
- **Assemble fragmented messages** (WebSocket messages can span multiple frames)
- Parse complete JSON messages
- Extract audio/text/interruption signals
- Raise appropriate events

**Response Message Format:**
```json
{
  "serverContent": {
    "interrupted": false,
    "modelTurn": {
      "parts": [
        {
          "inlineData": {
            "mimeType": "audio/pcm;rate=24000",
            "data": "{base64_audio}"
          }
        }
      ]
    }
  }
}
```

**Implementation (with fragmented message handling):**
```csharp
private async Task ReceiveLoopAsync()
{
    var buffer = new byte[RECEIVE_BUFFER_SIZE];
    var messageBuffer = new MemoryStream();
    
    try
    {
        while (_webSocket?.State == WebSocketState.Open && 
               !_cts!.Token.IsCancellationRequested)
        {
            var result = await _webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                _cts.Token
            );
            
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await DisconnectAsync();
                break;
            }
            
            if (result.MessageType == WebSocketMessageType.Text)
            {
                // Append to message buffer
                messageBuffer.Write(buffer, 0, result.Count);
                
                // Only process when we have the complete message
                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(
                        messageBuffer.GetBuffer(), 
                        0, 
                        (int)messageBuffer.Length
                    );
                    
                    // Reset buffer for next message
                    messageBuffer.SetLength(0);
                    
                    ProcessMessage(json);
                }
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Normal shutdown - don't report as error
    }
    catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
    {
        // Connection lost - attempt reconnect
        _ = HandleDisconnectionAsync();
    }
    catch (Exception ex)
    {
        SetState(ConnectionState.Error);
        ErrorOccurred?.Invoke(this, $"Receive error: {ex.Message}");
    }
    finally
    {
        messageBuffer.Dispose();
    }
}

private async Task HandleDisconnectionAsync()
{
    if (_disposed) return;
    
    SetState(ConnectionState.Reconnecting);
    
    // Clean up old connection
    _webSocket?.Dispose();
    _webSocket = null;
    
    // Attempt reconnect if we have an agent
    if (_currentAgent != null)
    {
        await Task.Delay(1000); // Brief delay before reconnect
        await ConnectAsync(_currentAgent);
    }
    else
    {
        SetState(ConnectionState.Disconnected);
    }
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
            System.Diagnostics.Debug.WriteLine("[Gemini] Setup complete");
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
                AudioReceived?.Invoke(this, pcmData);
            }
            catch (FormatException)
            {
                System.Diagnostics.Debug.WriteLine("[Gemini] Invalid base64 audio data");
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
```

#### 3.6 Interruption Handling

**When User Speaks During AI Response:**
1. API sends `interrupted: true` in response
2. Raise `Interrupted` event
3. `MainViewModel` calls `IAudioService.InterruptPlaybackAsync()`
4. Stop all audio playback immediately
5. Clear audio buffer queue

**Implementation:**
- Already handled in `ProcessMessage` above
- `MainViewModel` subscribes to `Interrupted` event

#### 3.7 Disconnection

**Clean Shutdown:**
```csharp
public async Task DisconnectAsync()
{
    _cts?.Cancel();
    
    if (_webSocket?.State == WebSocketState.Open)
    {
        await _webSocket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "User disconnected",
            CancellationToken.None
        );
    }
    
    _webSocket?.Dispose();
    _webSocket = null;
    
    State = ConnectionState.Disconnected;
    ConnectionStateChanged?.Invoke(this, State);
}
```

**Files to Create:**
- `src/GaimerDesktop/GaimerDesktop/Services/GeminiLiveService.cs`

**Files to Modify:**
- `src/GaimerDesktop/GaimerDesktop/Services/IGeminiService.cs` (add ErrorOccurred)
- `src/GaimerDesktop/GaimerDesktop/Services/MockGeminiService.cs` (update interface)
- `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs` (register GeminiLiveService)
- `src/GaimerDesktop/GaimerDesktop/Models/Agent.cs` (ensure SystemInstruction property exists)

**Reference Implementation:** See `GAIMER_DESKTOP_APP_SPEC.md` Section 6.2

**Acceptance Criteria:**
- Connects to Gemini API successfully
- Sends setup message with agent system prompt
- Transmits audio chunks correctly
- Transmits image frames correctly
- Receives and processes audio responses
- Handles interruptions correctly
- Disconnects cleanly
- Handles errors gracefully

---

### Task 4: MainViewModel Integration

**Goal:** Wire up GeminiLiveService with audio and capture services

**File:** `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs`

**Current State Analysis:**

The MainViewModel already has:
- Event subscription for `_geminiService.ConnectionStateChanged`
- Event subscription for `_geminiService.TextReceived`
- Event subscription for `_audioService.VolumeChanged` and `ErrorOccurred`
- Event subscription for `_captureService.FrameCaptured`
- Placeholder callback in `StartRecordingAsync`: `_ => { /* Phase 3 will enqueue/send to Gemini */ }`

**What needs to change:**

#### 4.1 Update Audio Callback to Send to Gemini

The Phase 2 audio service uses callback-based recording. Replace the placeholder:

```csharp
// In ToggleConnectionAsync(), replace:
await _audioService.StartRecordingAsync(_ => { /* Phase 3 will enqueue/send to Gemini */ });

// With:
await _audioService.StartRecordingAsync(OnAudioCaptured);
```

Add the callback method:
```csharp
private void OnAudioCaptured(byte[] pcmData)
{
    // Non-blocking send - callback must return quickly
    if (_geminiService.IsConnected)
    {
        _ = _geminiService.SendAudioAsync(pcmData);
    }
}
```

#### 4.2 Wire Up Frame Capture to Send Images

The current `FrameCaptured` event subscription only updates the preview. Extend it:

```csharp
// In constructor, update existing subscription:
_captureService.FrameCaptured += (_, frame) =>
{
    PreviewImage = frame;
    
    // Send to Gemini if connected
    if (_geminiService.IsConnected)
    {
        _ = _geminiService.SendImageAsync(frame);
    }
};
```

#### 4.3 Add Audio Response Handling

Add subscription in constructor:
```csharp
_geminiService.AudioReceived += (_, pcmData) =>
{
    // Queue audio for playback
    _ = _audioService.PlayAudioAsync(pcmData);
};
```

#### 4.4 Add Interruption Handling

Add subscription in constructor:
```csharp
_geminiService.Interrupted += (_, _) =>
{
    // User spoke during AI response - stop playback immediately
    _ = _audioService.InterruptPlaybackAsync();
};
```

#### 4.5 Add Error Handling

Add subscription in constructor:
```csharp
_geminiService.ErrorOccurred += (_, message) =>
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        // Surface error to user via status message or alert
        System.Diagnostics.Debug.WriteLine($"[Gemini Error] {message}");
        
        // Optionally show in UI
        AiDisplayContent = new AiDisplayContent
        {
            Text = $"⚠️ {message}"
        };
        OnPropertyChanged(nameof(HasAiContent));
        OnPropertyChanged(nameof(HasNoAiContent));
    });
};
```

#### 4.6 Handle Reconnecting State

Update the `ConnectionBadgeColor` and `ConnectionBadgeText` computed properties:
```csharp
public string ConnectionBadgeText => ConnectionState switch
{
    ConnectionState.Disconnected => "OFFLINE",
    ConnectionState.Connecting => "CONNECTING",
    ConnectionState.Connected => "CONNECTED",
    ConnectionState.Reconnecting => "RECONNECTING",
    ConnectionState.Error => "ERROR",
    _ => "UNKNOWN"
};

public Color ConnectionBadgeColor => ConnectionState switch
{
    ConnectionState.Disconnected => Color.FromArgb("#6b7280"),
    ConnectionState.Connecting => Color.FromArgb("#eab308"),
    ConnectionState.Connected => Color.FromArgb("#22c55e"),
    ConnectionState.Reconnecting => Color.FromArgb("#eab308"),  // Yellow like Connecting
    ConnectionState.Error => Color.FromArgb("#ef4444"),
    _ => Color.FromArgb("#6b7280")
};
```

**Files to Modify:**
- `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs`

**Acceptance Criteria:**
- Audio callback sends PCM data to Gemini when connected
- **Baseline**: frame capture stores JPEG frames into a local **Visual Reel** (~1 FPS), and does **not** automatically send images continuously when merely connected
- **On-demand**: the agent can request **current** and **past** frames (time-indexed) from the reel and send only what is needed for a question
- **Live / High Attention (toggle)**: processing loop runs at **2–3 FPS** with **drop-frame/latest-frame-wins**, **diff-gating**, and **<2s** target turnaround; emits user-facing output **only on detection (strict + uncertainty)**
- Audio responses play back correctly via `PlayAudioAsync`
- Interruptions trigger `InterruptPlaybackAsync`
- Errors are surfaced to user
- Reconnecting state shows in UI

---

### Task 5: Error Handling & Reconnection

**Goal:** Implement robust error handling and reconnection logic

**Error Categories:**

1. **Connection Errors**
   - WebSocket connection failure
   - Timeout errors
   - Network errors

2. **API Errors**
   - Invalid API key
   - Rate limiting
   - Invalid message format

3. **State Errors**
   - Connection lost during transmission
   - Unexpected disconnection

**Reconnection Strategy:**

```csharp
private async Task ReconnectWithBackoffAsync()
{
    var delays = new[] { 1000, 2000, 5000, 10000, 30000 };
    
    for (int attempt = 0; attempt < delays.Length; attempt++)
    {
        State = ConnectionState.Reconnecting;
        ConnectionStateChanged?.Invoke(this, State);
        
        await Task.Delay(delays[attempt]);
        
        try
        {
            await ConnectAsync(SelectedAgent);
            if (IsConnected) return;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Reconnection attempt {attempt + 1} failed: {ex.Message}");
        }
    }
    
    State = ConnectionState.Error;
    ConnectionStateChanged?.Invoke(this, State);
    ErrorOccurred?.Invoke(this, "Failed to reconnect after multiple attempts");
}
```

**Implementation:**
- Add reconnection logic to `GeminiLiveService`
- Handle connection errors in receive loop
- Notify ViewModel of errors for user display

**Acceptance Criteria:**
- Reconnects automatically on connection loss
- Exponential backoff prevents API spam
- User notified of connection issues
- Graceful degradation on persistent errors

---

### Task 6: Update Dependency Injection

**Goal:** Register GeminiLiveService with configuration support and fallback to mock

**File:** `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs`

**Complete Updated MauiProgram.cs:**

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using GaimerDesktop.Services;
using GaimerDesktop.ViewModels;
using GaimerDesktop.Views;

namespace GaimerDesktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        // Build configuration from multiple sources
        var configuration = BuildConfiguration();
        builder.Services.AddSingleton<IConfiguration>(configuration);
        
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Orbitron-Bold.ttf", "OrbitronBold");
                fonts.AddFont("Orbitron-Regular.ttf", "OrbitronRegular");
                fonts.AddFont("Rajdhani-Regular.ttf", "RajdhaniRegular");
                fonts.AddFont("Rajdhani-SemiBold.ttf", "RajdhaniSemiBold");
                fonts.AddFont("Rajdhani-Bold.ttf", "RajdhaniBold");
            });

        RegisterServices(builder.Services, configuration);
        RegisterViewModels(builder.Services);
        RegisterViews(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
    
    private static IConfiguration BuildConfiguration()
    {
        var configBuilder = new ConfigurationBuilder();
        
        // User secrets (development) - optional, won't fail if not configured
        try
        {
            configBuilder.AddUserSecrets<App>(optional: true, reloadOnChange: false);
        }
        catch
        {
            // User secrets not available on this platform - that's OK
        }
        
        // Environment variables with GEMINI_ prefix
        // GEMINI_APIKEY -> configuration["APIKEY"]
        configBuilder.AddEnvironmentVariables("GEMINI_");
        
        return configBuilder.Build();
    }

    private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Audio services (Phase 2 complete)
#if WINDOWS
        services.AddSingleton<IAudioService, Platforms.Windows.AudioService>();
#elif MACCATALYST
        services.AddSingleton<IAudioService, Platforms.MacCatalyst.AudioService>();
#else
        services.AddSingleton<IAudioService, MockAudioService>();
#endif

        // Window capture (still mock - Phase 1 blocker)
        services.AddSingleton<IWindowCaptureService, MockWindowCaptureService>();
        
        // Gemini service - real or mock based on API key availability
        var apiKey = configuration["GeminiApiKey"] ?? configuration["APIKEY"];
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            // Real service when API key is configured
            services.AddSingleton<IGeminiService, GeminiLiveService>();
        }
        else
        {
            // Fallback to mock for development without API key
            System.Diagnostics.Debug.WriteLine(
                "[Gaimer] No API key found. Using MockGeminiService. " +
                "See config.example.txt for setup instructions.");
            services.AddSingleton<IGeminiService, MockGeminiService>();
        }
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<AgentSelectionViewModel>();
        services.AddSingleton<MainViewModel>();
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddTransient<AgentSelectionPage>();
        services.AddTransient<MainPage>();
        services.AddTransient<MinimalViewPage>();
    }
}
```

**Package Reference Required:**

Add to `GaimerDesktop.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="8.0.0" />
</ItemGroup>
```

**Acceptance Criteria:**
- Configuration loaded from user-secrets and environment variables
- `GeminiLiveService` registered when API key is available
- `MockGeminiService` fallback when no API key (with debug message)
- `IConfiguration` available for injection into `GeminiLiveService`
- Build succeeds on all platforms

---

## Task Summary

| Task | Description | Files | Status |
|------|-------------|-------|--------|
| Task 1 | API Configuration Setup | `MauiProgram.cs`, `GaimerDesktop.csproj`, `config.example.txt` | ✅ Done |
| Task 2 | Update Interfaces/Enums | `ConnectionState.cs`, `IGeminiService.cs`, `MockGeminiService.cs`, `MainViewModel.cs` | ✅ Done |
| Task 3 | GeminiLiveService Implementation | `Services/GeminiLiveService.cs` (new) | ✅ Done |
| Task 4 | MainViewModel Integration | `ViewModels/MainViewModel.cs` | ✅ Done |
| Task 5 | Error Handling & Reconnection | `Services/GeminiLiveService.cs` | ✅ Done |
| Task 6 | Update Dependency Injection | `MauiProgram.cs` | ✅ Done |

---

## Testing Checklist

### Pre-Testing Requirements
- [ ] API key configured (user-secrets or environment variable)
- [ ] Audio system validated (Phase 2 testing complete)
- [ ] App builds and runs on target platform

### Connection Tests
- [ ] Connects to Gemini API successfully with valid API key
- [ ] Shows "API key not configured" error when missing
- [ ] Handles invalid API key gracefully (401/403 response)
- [ ] Handles network timeout gracefully
- [ ] Handles network errors gracefully
- [ ] Reconnects on connection loss (Reconnecting state visible)
- [ ] Disconnects cleanly without lingering resources
- [ ] Connection state badge updates correctly in UI

### Audio Transmission Tests
- [ ] Audio callback sends PCM to Gemini when connected
- [ ] Audio chunks sent correctly (base64, `audio/pcm;rate=16000` MIME)
- [ ] No audio loss during transmission
- [ ] Audio not sent when disconnected
- [ ] Handles send errors gracefully (connection dropped mid-send)

### Image Transmission Tests
- [ ] Frame capture sends JPEG to Gemini at 1 FPS when connected
- [ ] Images sent correctly (base64, `image/jpeg` MIME)
- [ ] Large images handled correctly (mock: ~100KB JPEG)
- [ ] Images not sent when disconnected
- [ ] Handles send errors gracefully

### Response Handling Tests
- [ ] Audio responses received and decoded from base64
- [ ] Audio played back correctly via `PlayAudioAsync`
- [ ] Audio sample rate correct (24kHz)
- [ ] Text responses display in AI Insights panel
- [ ] Interruptions trigger `InterruptPlaybackAsync` immediately
- [ ] Fragmented WebSocket messages assembled correctly
- [ ] Multiple response chunks handled in sequence

### Integration Tests
- [ ] End-to-end voice conversation works (speak → AI responds)
- [ ] Visual analysis works (mock images sent → AI comments)
- [ ] Interruption during AI response works (speak → playback stops)
- [ ] MainView → MinimalView transition maintains connection
- [ ] Disconnect from MinimalView returns to MainView correctly
- [ ] Session teardown cleans up all resources

### Error Handling Tests
- [ ] API key missing → clear error message
- [ ] Network timeout → error surfaced to user
- [ ] Connection dropped → Reconnecting state → auto-reconnect attempt
- [ ] Multiple reconnect failures → Error state
- [ ] WebSocket error → ErrorOccurred event raised

### Performance Tests
- [ ] Audio transmission latency acceptable (<100ms)
- [ ] Response playback latency acceptable (<100ms)
- [ ] No memory leaks during 10+ minute session
- [ ] CPU usage reasonable (<20% during active session)
- [ ] Network bandwidth usage acceptable (~50KB/s audio up, ~100KB/s audio down)

---

## API Configuration Reference

### WebSocket Endpoint
```
wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={API_KEY}
```

### Model
```
gemini-2.5-flash-preview-native-audio-dialog
```

### Voice Options
- `Fenrir` (default, recommended)
- Other voices available via API

### MIME Types
- Audio Input: `audio/pcm;rate=16000`
- Audio Output: `audio/pcm;rate=24000`
- Image: `image/jpeg`

---

## Implementation Order

**Recommended execution sequence:**

1. **Task 1:** API Configuration Setup
   - Add package references
   - Update MauiProgram.cs with configuration builder
   - Test that configuration loads correctly

2. **Task 2:** Update Interfaces and Enums
   - Add `Reconnecting` to ConnectionState
   - Update IGeminiService with ErrorOccurred, IsConnected, IDisposable
   - Update MockGeminiService
   - Update MainViewModel switch statements

3. **Task 6:** Update Dependency Injection
   - Register GeminiLiveService conditionally
   - Verify mock fallback works

4. **Task 3:** GeminiLiveService Implementation
   - Create the class file
   - Implement connection logic
   - Implement setup message
   - Implement audio/image sending
   - Implement receive loop with message assembly

5. **Task 4:** MainViewModel Integration
   - Wire audio callback to send to Gemini
   - Wire frame capture to send images
   - Add AudioReceived subscription
   - Add Interrupted subscription
   - Add ErrorOccurred subscription

6. **Task 5:** Error Handling & Reconnection
   - Test error scenarios
   - Refine reconnection logic if needed

---

## Next Steps After Phase 3

Once integration is complete:

1. **Phase 4: Polish** - UI improvements, audio visualizer animation (SkiaSharp)
2. **Real Window Capture** - Implement platform-specific window capture (Phase 1 blocker)
3. **Performance Optimization** - Reduce latency, optimize bandwidth
4. **End-to-End Testing** - Test with real games and applications

---

## Documentation Updates

After completing Phase 3, update:

- `GAIMER/GameGhost/PROGRESS_LOG.md` - Mark Phase 3 tasks complete
- `GAIMER/GameGhost/FEATURE_LIST.md` - Update network/integration feature status
- `GAIMER/GameGhost/BUG_FIX_LOG.md` - Document any issues found
- Code comments in `GeminiLiveService`

---

## Troubleshooting Guide

### Common Issues

1. **"API Key not configured"**
   - Set user-secrets: `dotnet user-secrets set "GeminiApiKey" "key"`
   - Or set environment variable: `GEMINI_APIKEY=your-key`
   - Restart the app after setting

2. **"Connection timeout"**
   - Check internet connection
   - Verify API key is valid at https://aistudio.google.com/app/apikey
   - Check firewall/proxy settings
   - Try increasing `CONNECT_TIMEOUT_MS`

3. **"WebSocket closed unexpectedly"**
   - Check for rate limiting (too many requests)
   - Verify API key has Gemini Live access
   - Check for API quota limits

4. **"Invalid JSON response"**
   - Enable Debug logging to see raw messages
   - Check API version compatibility
   - Verify message format matches Gemini Live spec

5. **"Audio not playing"**
   - Verify audio service is working (test with Phase 2)
   - Check sample rate (24kHz expected from API)
   - Verify base64 decoding succeeds
   - Check `AudioReceived` event is wired up

6. **"Images not being analyzed"**
   - Verify mock capture service is sending frames
   - Check `FrameCaptured` event subscription
   - Verify connection is established before capture starts
   - Check image format (JPEG, ~60% quality)

7. **"Interruption not working"**
   - Verify `Interrupted` event subscription exists
   - Check that `InterruptPlaybackAsync` is being called
   - Test with Phase 2 audio service directly

---

## Files to Create

| File | Description |
|------|-------------|
| `Services/GeminiLiveService.cs` | WebSocket client implementation |

## Files to Modify

| File | Changes |
|------|---------|
| `Models/ConnectionState.cs` | Add `Reconnecting` |
| `Services/IGeminiService.cs` | Add `ErrorOccurred`, `IsConnected`, `IDisposable` |
| `Services/MockGeminiService.cs` | Implement new interface members |
| `ViewModels/MainViewModel.cs` | Wire audio/image/response handling |
| `MauiProgram.cs` | Add configuration, register GeminiLiveService |
| `GaimerDesktop.csproj` | Add configuration packages |
| `config.example.txt` | Update with setup instructions |

---

---

## Deferred Improvements (Phase 4 / Polish)

The following improvements were identified during Phase 2/3 testing and are deferred to Phase 4:

### User-Facing Error Messages for Provider Issues
- **Context:** When a provider (e.g., Gemini API) has account/billing issues, quota limits, or connection failures, the user should see a friendly message instead of silent failure.
- **Proposed UX:** Display message like "Agent is currently unavailable for live chat" or "Connection to AI service failed - please try again later"
- **Implementation Notes:**
  - Tie error handling to specific agent/provider combinations
  - Consider showing error in the AI Insights panel or a toast notification
  - Log detailed error for debugging while showing friendly message to user
- **Priority:** Medium (improves user experience; not blocking core functionality)

---

**Notes:**
- WebSocket implementation is critical for real-time interaction
- Test thoroughly with real API before moving to Phase 4
- Keep mock service available for offline development (no API key = mock)
- Monitor API usage and rate limits
- The Phase 2 audio system must be validated on-device before Phase 3 testing

