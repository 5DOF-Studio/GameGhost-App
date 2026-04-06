using System.Diagnostics;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Local;

/// <summary>
/// Composing ILocalAudioConversationClient that wires three independent layers:
///   Layer 1 (STT): Optional ISpeechToTextProvider — converts mic PCM to text.
///   Layer 2 (Conversational intelligence): injected ILocalTextConversationBackend.
///   Layer 3 (TTS): Optional ITextToSpeechProvider — converts response text to PCM.
///
/// When STT/TTS are provided and available, SendAudioAsync performs the full
/// turn-based pipeline: audio -> STT -> backend -> TTS -> AudioReceived.
/// When unavailable, graceful degradation: errors for audio input, text-only for output.
/// SendTextAsync always works as a first-class fallback.
/// </summary>
public sealed class LocalVoiceConversationClient : ILocalAudioConversationClient
{
    private readonly ILocalTextConversationBackend _backend;
    private readonly ISpeechToTextProvider? _stt;
    private readonly ITextToSpeechProvider? _tts;
    private readonly List<ConversationMessage> _history = new();
    private readonly object _lock = new();
    private bool _disposed;

    private const int MaxUserAssistantTurns = 20;

    /// <summary>
    /// Creates a local voice client with optional STT/TTS providers.
    /// Without STT/TTS, SendAudioAsync surfaces an error; SendTextAsync still works.
    /// </summary>
    public LocalVoiceConversationClient(
        ILocalTextConversationBackend backend,
        ISpeechToTextProvider? stt = null,
        ITextToSpeechProvider? tts = null)
    {
        _backend = backend;
        _stt = stt;
        _tts = tts;
    }

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TextReceived;
    public event EventHandler? Interrupted;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected { get; private set; }
    public string RuntimeName => _backend.RuntimeName;

    /// <summary>Whether speech-to-text input is available.</summary>
    public bool SpeechInputAvailable => _stt?.IsAvailable == true;

    /// <summary>Whether text-to-speech output is available.</summary>
    public bool SpeechOutputAvailable => _tts?.IsAvailable == true;

    public Task ConnectAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _history.Clear();
            _history.Add(new ConversationMessage("system", agent.ComposedPersonality));
            IsConnected = true;
        }

        ConnectionStateChanged?.Invoke(this, ConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _history.Clear();
            IsConnected = false;
        }

        ConnectionStateChanged?.Invoke(this, ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public async Task SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (!IsConnected)
        {
            const string message = "Cannot send text: local voice provider is not connected.";
            ErrorOccurred?.Invoke(this, message);
            throw new InvalidOperationException(message);
        }

        List<ConversationMessage> snapshot;
        lock (_lock)
        {
            _history.Add(new ConversationMessage("user", text));
            TrimHistory();
            snapshot = new List<ConversationMessage>(_history);
        }

        try
        {
            var response = await _backend.SendAsync(snapshot, cancellationToken).ConfigureAwait(false);

            lock (_lock)
            {
                _history.Add(new ConversationMessage("assistant", response));
                TrimHistory();
            }

            TextReceived?.Invoke(this, response);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[LocalVoice] SendTextAsync error: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Local voice error: {ex.Message}");
        }
    }

    public async Task SendAudioAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        // TODO(17-STT): Audio accumulation needed before calling STT.
        // Each mic tap is ~640 bytes (20ms at 16kHz). SFSpeechRecognizer crashes on
        // sub-second buffers (EXC_BAD_INSTRUCTION in AudioConverter). Accumulate a
        // minimum duration (e.g. 1-2s after VAD silence) before calling TranscribeAsync.
        // Remove this guard once the accumulation buffer is implemented.
        Debug.WriteLine($"[LocalVoice] SendAudioAsync — skipping STT (accumulation not implemented, {audioData.Length} bytes).");
        return;

        // Step 1: Check STT availability
        if (_stt is null || !_stt.IsAvailable)
        {
            Debug.WriteLine("[LocalVoice] SendAudioAsync — STT unavailable.");
            ErrorOccurred?.Invoke(this, "Speech recognition is not available for local voice.");
            return;
        }

        try
        {
            // Step 2: STT — convert audio to text
            var transcript = await _stt.TranscribeAsync(audioData, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                Debug.WriteLine("[LocalVoice] SendAudioAsync — no transcript from STT.");
                ErrorOccurred?.Invoke(this, "Could not transcribe audio — no speech detected.");
                return;
            }

            Debug.WriteLine($"[LocalVoice] STT transcript: {transcript}");

            // Step 3: Forward transcript to backend (same path as SendTextAsync)
            List<ConversationMessage> snapshot;
            lock (_lock)
            {
                _history.Add(new ConversationMessage("user", transcript));
                TrimHistory();
                snapshot = new List<ConversationMessage>(_history);
            }

            var response = await _backend.SendAsync(snapshot, cancellationToken).ConfigureAwait(false);

            lock (_lock)
            {
                _history.Add(new ConversationMessage("assistant", response));
                TrimHistory();
            }

            // Step 4: Always emit text response for visibility
            TextReceived?.Invoke(this, response);

            // Step 5: TTS — convert response to audio (if available)
            if (_tts is not null && _tts.IsAvailable)
            {
                var pcmAudio = await _tts.SynthesizeAsync(response, cancellationToken).ConfigureAwait(false);
                if (pcmAudio is not null && pcmAudio.Length > 0)
                {
                    AudioReceived?.Invoke(this, pcmAudio);
                }
            }
            else
            {
                Debug.WriteLine("[LocalVoice] TTS unavailable — text-only response.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[LocalVoice] SendAudioAsync error: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Local voice error: {ex.Message}");
        }
    }

    public Task SendContextualUpdateAsync(string contextText, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _history.Add(new ConversationMessage("system", contextText));
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            _history.Clear();
            IsConnected = false;
        }
    }

    /// <summary>
    /// Trims oldest user/assistant pairs when history exceeds MaxUserAssistantTurns.
    /// System messages are always preserved.
    /// </summary>
    private void TrimHistory()
    {
        var userAssistantCount = _history.Count(m => m.Role is "user" or "assistant");
        if (userAssistantCount <= MaxUserAssistantTurns * 2)
            return;

        var toRemove = userAssistantCount - MaxUserAssistantTurns * 2;
        var removed = 0;
        for (var i = 0; i < _history.Count && removed < toRemove; i++)
        {
            if (_history[i].Role is "user" or "assistant")
            {
                _history.RemoveAt(i);
                i--;
                removed++;
            }
        }
    }
}
