using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

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

