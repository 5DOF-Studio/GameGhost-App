using Pv;

namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Picovoice Porcupine wake word detector. Processes raw 16kHz mono PCM audio.
/// Requires PICOVOICE_ACCESS_KEY environment variable.
/// When unavailable (no key, init failure), IsAvailable = false and ProcessAudio is a no-op.
/// </summary>
public sealed class PorcupineWakeDetector : IPorcupineWakeDetector
{
    private readonly Porcupine? _porcupine;
    private readonly string[] _keywordNames;
    private readonly List<short> _buffer = new();
    private readonly object _lock = new();
    private bool _disposed;

    public bool IsAvailable { get; }

    public event EventHandler<string>? WakeWordDetected;

    /// <summary>
    /// Initialize Porcupine with the given access key and keyword configuration.
    /// </summary>
    /// <param name="accessKey">Picovoice access key. Null = unavailable.</param>
    /// <param name="keywordPaths">Custom .ppn keyword file paths. Null = use built-in "PORCUPINE" for testing.</param>
    /// <param name="keywordNames">Human-readable names matching keywordPaths order. Used in WakeWordDetected event.</param>
    /// <param name="sensitivities">Detection sensitivity per keyword (0.0-1.0). Null = default 0.5.</param>
    public PorcupineWakeDetector(
        string? accessKey,
        IReadOnlyList<string>? keywordPaths = null,
        IReadOnlyList<string>? keywordNames = null,
        IReadOnlyList<float>? sensitivities = null)
    {
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            System.Diagnostics.Debug.WriteLine("[Porcupine] No access key — wake word detection unavailable. Set PICOVOICE_ACCESS_KEY env var.");
            IsAvailable = false;
            _keywordNames = Array.Empty<string>();
            return;
        }

        try
        {
            if (keywordPaths is { Count: > 0 })
            {
                // Custom wake words (e.g., "Hey Leroy", "Hey Wasp")
                _porcupine = Porcupine.FromKeywordPaths(
                    accessKey,
                    keywordPaths.ToList(),
                    sensitivities: sensitivities?.ToList());
                _keywordNames = keywordNames?.ToArray() ?? keywordPaths.Select(Path.GetFileNameWithoutExtension).ToArray()!;
            }
            else
            {
                // Built-in keyword for testing
                _porcupine = Porcupine.FromBuiltInKeywords(
                    accessKey,
                    new List<BuiltInKeyword> { BuiltInKeyword.PORCUPINE });
                _keywordNames = new[] { "PORCUPINE" };
            }

            IsAvailable = true;
            System.Diagnostics.Debug.WriteLine($"[Porcupine] Initialized. Keywords: [{string.Join(", ", _keywordNames)}], FrameLength: {_porcupine.FrameLength}, SampleRate: {_porcupine.SampleRate}");
        }
        catch (PorcupineException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Porcupine] Initialization failed: {ex.Message}");
            IsAvailable = false;
            _keywordNames = Array.Empty<string>();
        }
    }

    public void ProcessAudio(byte[] pcm16Data)
    {
        if (!IsAvailable || _porcupine == null || pcm16Data.Length < 2) return;

        // Convert byte[] (PCM16 little-endian) to short[]
        var samples = new short[pcm16Data.Length / 2];
        Buffer.BlockCopy(pcm16Data, 0, samples, 0, pcm16Data.Length);

        lock (_lock)
        {
            if (_disposed) return;

            _buffer.AddRange(samples);

            // Process complete frames
            var frameLength = _porcupine.FrameLength;
            while (_buffer.Count >= frameLength)
            {
                var frame = _buffer.GetRange(0, frameLength).ToArray();
                _buffer.RemoveRange(0, frameLength);

                try
                {
                    var keywordIndex = _porcupine.Process(frame);
                    if (keywordIndex >= 0 && keywordIndex < _keywordNames.Length)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Porcupine] Wake word detected: {_keywordNames[keywordIndex]}");
                        WakeWordDetected?.Invoke(this, _keywordNames[keywordIndex]);
                    }
                }
                catch (PorcupineException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Porcupine] Process error: {ex.Message}");
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _buffer.Clear();
        }
        _porcupine?.Dispose();
    }
}
