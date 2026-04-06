using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WitnessDesktop.Services.Local;

/// <summary>
/// Local runtime adapter for Ollama. Uses the official local HTTP API to detect
/// runtime reachability and whether the configured MiniCPM vision model is installed.
/// Voice remains unavailable in this adapter until a production-safe local audio path lands.
/// </summary>
public sealed class OllamaLocalModelRuntime : ILocalModelRuntime
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly bool _voiceEnabled;
    private readonly ISpeechToTextProvider? _stt;
    private readonly ITextToSpeechProvider? _tts;

    public OllamaLocalModelRuntime(
        HttpClient httpClient,
        string modelId,
        bool voiceEnabled = false,
        ISpeechToTextProvider? stt = null,
        ITextToSpeechProvider? tts = null)
    {
        _httpClient = httpClient;
        _modelId = string.IsNullOrWhiteSpace(modelId) ? "minicpm-v" : modelId;
        _voiceEnabled = voiceEnabled;
        _stt = stt;
        _tts = tts;
    }

    public async Task<LocalRuntimeHealth> GetHealthAsync(CancellationToken ct = default)
    {
        Console.WriteLine("[Preflight] ── Local Runtime Preflight ──────────────────");
        Console.WriteLine($"[Preflight]  Runtime: Ollama ({_httpClient.BaseAddress})");
        Console.WriteLine($"[Preflight]  Model:   {_modelId}");

        try
        {
            var response = await _httpClient.GetAsync("/api/tags", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var reason = $"tags query failed ({(int)response.StatusCode})";
                Console.WriteLine($"[Preflight]  [FAIL] Ollama reachable but returned {(int)response.StatusCode}");
                Console.WriteLine("[Preflight] ──────────────────────────────────────────");
                return WithSpeechInfo(new LocalRuntimeHealth
                {
                    RuntimeAvailable = false,
                    BrainAvailable = false,
                    VoiceAvailable = false,
                    RuntimeName = "ollama",
                    ModelId = _modelId,
                    FailureReason = reason
                });
            }

            Console.WriteLine("[Preflight]  [OK]   Ollama is running");

            var payload = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: ct).ConfigureAwait(false);
            if (payload?.Models is null)
            {
                Console.WriteLine("[Preflight]  [FAIL] Ollama returned empty model list");
                Console.WriteLine("[Preflight] ──────────────────────────────────────────");
                return WithSpeechInfo(new LocalRuntimeHealth
                {
                    RuntimeAvailable = true,
                    BrainAvailable = false,
                    VoiceAvailable = false,
                    RuntimeName = "ollama",
                    ModelId = _modelId,
                    FailureReason = "ollama tags response was empty"
                });
            }

            var modelInstalled = payload.Models.Any(IsConfiguredModel);

            if (modelInstalled)
            {
                Console.WriteLine($"[Preflight]  [OK]   Model '{_modelId}' is installed");
            }
            else
            {
                var installed = string.Join(", ", payload.Models.Select(m => m.Name ?? m.Model ?? "?"));
                Console.WriteLine($"[Preflight]  [FAIL] Model '{_modelId}' NOT found. Installed: [{installed}]");
                Console.WriteLine($"[Preflight]  Fix:   Run 'ollama pull {_modelId}' to install the model.");
            }

            var health = WithSpeechInfo(new LocalRuntimeHealth
            {
                RuntimeAvailable = true,
                BrainAvailable = modelInstalled,
                VoiceAvailable = _voiceEnabled && modelInstalled,
                RuntimeName = "ollama",
                ModelId = _modelId,
                FailureReason = modelInstalled ? null : $"model '{_modelId}' not installed in Ollama"
            });

            Console.WriteLine($"[Preflight]  STT:   {(health.SpeechInputAvailable ? "Available" : "Unavailable")} ({health.SttEngineName ?? "none"})");
            Console.WriteLine($"[Preflight]  TTS:   {(health.SpeechOutputAvailable ? "Available" : "Unavailable")} ({health.TtsEngineName ?? "none"})");
            Console.WriteLine("[Preflight] ──────────────────────────────────────────");

            return health;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Preflight]  [FAIL] Ollama not reachable — {ex.Message}");
            Console.WriteLine("[Preflight]  Fix:   Run 'ollama serve' to start the Ollama runtime.");
            Console.WriteLine("[Preflight] ──────────────────────────────────────────");
            return WithSpeechInfo(new LocalRuntimeHealth
            {
                RuntimeAvailable = false,
                BrainAvailable = false,
                VoiceAvailable = false,
                RuntimeName = "ollama",
                ModelId = _modelId,
                FailureReason = ex.Message
            });
        }
    }

    private LocalRuntimeHealth WithSpeechInfo(LocalRuntimeHealth health)
    {
        return new LocalRuntimeHealth
        {
            RuntimeAvailable = health.RuntimeAvailable,
            BrainAvailable = health.BrainAvailable,
            VoiceAvailable = health.VoiceAvailable,
            RuntimeName = health.RuntimeName,
            ModelId = health.ModelId,
            FailureReason = health.FailureReason,
            SpeechInputAvailable = _stt?.IsAvailable == true,
            SpeechOutputAvailable = _tts?.IsAvailable == true,
            SttEngineName = _stt?.EngineName,
            TtsEngineName = _tts?.EngineName
        };
    }

    private bool IsConfiguredModel(OllamaModelInfo model)
    {
        return string.Equals(model.Name, _modelId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(model.Model, _modelId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(model.Name, $"{_modelId}:latest", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(model.Model, $"{_modelId}:latest", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo>? Models { get; init; }
    }

    private sealed class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("model")]
        public string? Model { get; init; }
    }
}
