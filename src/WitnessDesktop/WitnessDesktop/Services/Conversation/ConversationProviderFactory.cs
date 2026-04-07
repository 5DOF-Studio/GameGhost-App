using Microsoft.Extensions.Configuration;
using WitnessDesktop.Models;
using WitnessDesktop.Services.Conversation.Providers;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Services.Conversation;

/// <summary>
/// Factory for creating the appropriate <see cref="IConversationProvider"/> based on configuration.
/// </summary>
/// <remarks>
/// <para>
/// Provider selection priority:
/// <list type="number">
/// <item>If <c>InferenceMode</c> is <c>LocalOnly</c>, use the local provider and do not allow cloud voice overrides.</item>
/// <item>If <c>VOICE_PROVIDER</c> env var is set, use that provider explicitly.</item>
/// <item>If <c>USE_MOCK_SERVICES=true</c>, use mock provider.</item>
/// <item>If <c>ISettingsService.VoiceProvider</c> is set and matching API key exists, use that provider.</item>
/// <item>Auto-detect: if <c>OPENAI_APIKEY</c> is present, use OpenAI.</item>
/// <item>Auto-detect: if <c>GEMINI_APIKEY</c> (or variants) is present, use Gemini.</item>
/// <item>Fall back to mock provider.</item>
/// </list>
/// </para>
/// <para>
/// <b>Design Decision:</b> Only multimodal providers (supporting text + audio + images/video) are supported.
/// This enables full visual coaching capabilities where the AI can see game screenshots and provide context-aware guidance.
/// </para>
/// </remarks>
public sealed class ConversationProviderFactory
{
    private readonly IConfiguration _configuration;
    private readonly ISettingsService? _settings;
    private readonly ILocalAudioConversationClient? _localAudioClient;

    public ConversationProviderFactory(
        IConfiguration configuration,
        ISettingsService? settings = null,
        ILocalAudioConversationClient? localAudioClient = null)
    {
        _configuration = configuration;
        _settings = settings;
        _localAudioClient = localAudioClient;
    }

    /// <summary>
    /// Creates and returns the appropriate conversation provider based on environment configuration.
    /// </summary>
    public IConversationProvider Create()
    {
        var inferenceMode = _settings?.InferenceMode ?? InferenceMode.CloudOnly;
        var explicitProvider = _configuration["VOICE_PROVIDER"]?.ToLowerInvariant();
        var useMockServices = string.Equals(_configuration["USE_MOCK_SERVICES"], "true", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(_configuration["USE_MOCK_SERVICES"], "1", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"[ConversationProviderFactory] Entry: VOICE_PROVIDER='{explicitProvider ?? "null"}', USE_MOCK_SERVICES={useMockServices}, InferenceMode={_settings?.InferenceMode}, localAudioClient={(_localAudioClient != null ? "present" : "null")}");

        // LocalOnly is a hard routing rule: never allow cloud voice selection to override it.
        if (inferenceMode == InferenceMode.LocalOnly && _localAudioClient is not null)
        {
            LogProviderSelection("LocalMiniCpmConversationProvider", "inference mode=LocalOnly");
            return CreateLocalProvider();
        }

        // Explicit provider selection via VOICE_PROVIDER env var
        if (!string.IsNullOrEmpty(explicitProvider))
        {
            return explicitProvider switch
            {
                "gemini" => CreateGeminiProvider(),
                "openai" => CreateOpenAiProvider(),
                "local" => CreateLocalProvider(),
                "minicpm" => CreateLocalProvider(),
                "mock" => CreateMockProvider(),
                _ => throw new InvalidOperationException($"Unknown VOICE_PROVIDER: {explicitProvider}. Valid values: gemini, openai, local, minicpm, mock")
            };
        }

        // USE_MOCK_SERVICES override
        if (useMockServices)
        {
            LogProviderSelection("MockConversationProvider", "USE_MOCK_SERVICES=true");
            return CreateMockProvider();
        }

        Console.WriteLine($"[ConversationProviderFactory] InferenceMode={inferenceMode}, settings={(_settings != null ? "present" : "null")}, localAudioClient={(_localAudioClient != null ? "present" : "null")}");

        var settingsProvider = _settings?.VoiceProvider?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(settingsProvider))
        {
            if (settingsProvider == "gemini" && !string.IsNullOrEmpty(GetGeminiApiKey()))
            {
                LogProviderSelection("GeminiConversationProvider", "settings voice_provider=gemini");
                return CreateGeminiProvider();
            }

            if (settingsProvider == "openai" && !string.IsNullOrEmpty(GetOpenAiApiKey()))
            {
                LogProviderSelection("OpenAIConversationProvider", "settings voice_provider=openai");
                return CreateOpenAiProvider();
            }

            if ((settingsProvider == "local" || settingsProvider == "minicpm") && _localAudioClient is not null)
            {
                LogProviderSelection("LocalMiniCpmConversationProvider", $"settings voice_provider={settingsProvider}");
                return CreateLocalProvider();
            }
        }

        // Auto-detect: prefer OpenAI over Gemini (Gemini is cheaper but less mature for voice)
        var openAiKey = GetOpenAiApiKey();
        if (!string.IsNullOrEmpty(openAiKey))
        {
            return CreateOpenAiProvider();
        }

        var geminiKey = GetGeminiApiKey();
        if (!string.IsNullOrEmpty(geminiKey))
        {
            return CreateGeminiProvider();
        }

        // Fallback to mock
        LogProviderSelection("MockConversationProvider", "no API keys found");
        return CreateMockProvider();
    }

    private IConversationProvider CreateGeminiProvider()
    {
        var apiKey = GetGeminiApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("VOICE_PROVIDER=gemini but no Gemini API key found. Set GEMINI_APIKEY environment variable.");
        }

        var voice = GetVoiceName("gemini");
        LogProviderSelection("GeminiConversationProvider", $"GEMINI_APIKEY present, voice={voice}");
        return new GeminiConversationProvider(_configuration, voice);
    }

    private IConversationProvider CreateOpenAiProvider()
    {
        var apiKey = GetOpenAiApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("VOICE_PROVIDER=openai but no OpenAI API key found. Set OPENAI_APIKEY environment variable.");
        }

        var voice = GetVoiceName("openai");
        LogProviderSelection("OpenAIConversationProvider", $"OPENAI_APIKEY present, voice={voice}");
        return new OpenAIConversationProvider(_configuration, voice);
    }

    private static IConversationProvider CreateMockProvider()
    {
        return new MockConversationProvider();
    }

    private IConversationProvider CreateLocalProvider()
    {
        if (_localAudioClient is null)
        {
            throw new InvalidOperationException("VOICE_PROVIDER=local but no local audio client is registered.");
        }

        LogProviderSelection("LocalMiniCpmConversationProvider", $"runtime={_localAudioClient.RuntimeName}");
        return new LocalMiniCpmConversationProvider(_localAudioClient);
    }

    private string? GetGeminiApiKey()
    {
        return _configuration["GeminiApiKey"] ??
               _configuration["GEMINI_APIKEY"] ??
               _configuration["GEMINI_API_KEY"];
    }

    private string? GetOpenAiApiKey()
    {
        return _configuration["OPENAI_APIKEY"] ??
               _configuration["OPENAI_API_KEY"] ??
               _configuration["OpenAiApiKey"];
    }

    private string GetVoiceName(string provider)
    {
        var gender = _settings?.VoiceGender ?? "male";
        return VoiceConfig.GetVoiceName(provider, gender);
    }

    private static void LogProviderSelection(string providerName, string reason)
    {
        var message = $"[ConversationProviderFactory] Selected {providerName} ({reason})";
        Console.WriteLine(message);
        System.Diagnostics.Debug.WriteLine(message);
    }
}
