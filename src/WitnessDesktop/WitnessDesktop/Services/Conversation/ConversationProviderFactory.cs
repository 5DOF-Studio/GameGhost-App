using Microsoft.Extensions.Configuration;
using WitnessDesktop.Services.Conversation.Providers;

namespace WitnessDesktop.Services.Conversation;

/// <summary>
/// Factory for creating the appropriate <see cref="IConversationProvider"/> based on configuration.
/// </summary>
/// <remarks>
/// <para>
/// Provider selection priority:
/// <list type="number">
/// <item>If <c>VOICE_PROVIDER</c> env var is set, use that provider explicitly.</item>
/// <item>If <c>USE_MOCK_SERVICES=true</c>, use mock provider.</item>
/// <item>If <c>GEMINI_APIKEY</c> (or variants) is present, use Gemini.</item>
/// <item>If <c>OPENAI_APIKEY</c> is present, use OpenAI.</item>
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

    public ConversationProviderFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Creates and returns the appropriate conversation provider based on environment configuration.
    /// </summary>
    public IConversationProvider Create()
    {
        var explicitProvider = _configuration["VOICE_PROVIDER"]?.ToLowerInvariant();
        var useMockServices = string.Equals(_configuration["USE_MOCK_SERVICES"], "true", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(_configuration["USE_MOCK_SERVICES"], "1", StringComparison.OrdinalIgnoreCase);

        // Explicit provider selection via VOICE_PROVIDER env var
        if (!string.IsNullOrEmpty(explicitProvider))
        {
            return explicitProvider switch
            {
                "gemini" => CreateGeminiProvider(),
                "openai" => CreateOpenAiProvider(),
                "mock" => CreateMockProvider(),
                _ => throw new InvalidOperationException($"Unknown VOICE_PROVIDER: {explicitProvider}. Valid values: gemini, openai, mock")
            };
        }

        // USE_MOCK_SERVICES override
        if (useMockServices)
        {
            LogProviderSelection("MockConversationProvider", "USE_MOCK_SERVICES=true");
            return CreateMockProvider();
        }

        // Auto-detect based on available API keys
        var geminiKey = GetGeminiApiKey();
        if (!string.IsNullOrEmpty(geminiKey))
        {
            return CreateGeminiProvider();
        }

        var openAiKey = _configuration["OPENAI_APIKEY"] ?? _configuration["OPENAI_API_KEY"];
        if (!string.IsNullOrEmpty(openAiKey))
        {
            return CreateOpenAiProvider();
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

        LogProviderSelection("GeminiConversationProvider", "GEMINI_APIKEY present");
        return new GeminiConversationProvider(_configuration);
    }

    private IConversationProvider CreateOpenAiProvider()
    {
        var apiKey = GetOpenAiApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("VOICE_PROVIDER=openai but no OpenAI API key found. Set OPENAI_APIKEY environment variable.");
        }

        LogProviderSelection("OpenAIConversationProvider", "OPENAI_APIKEY present");
        return new OpenAIConversationProvider(_configuration);
    }

    private static IConversationProvider CreateMockProvider()
    {
        return new MockConversationProvider();
    }

    private string? GetGeminiApiKey()
    {
        return _configuration["GeminiApiKey"] ??
               _configuration["APIKEY"] ??      // from GEMINI_APIKEY prefix mapping
               _configuration["API_KEY"] ??     // from GEMINI_API_KEY prefix mapping
               _configuration["GEMINI_APIKEY"] ??
               _configuration["GEMINI_API_KEY"];
    }

    private string? GetOpenAiApiKey()
    {
        return _configuration["OPENAI_APIKEY"] ??
               _configuration["OPENAI_API_KEY"] ??
               _configuration["OpenAiApiKey"];
    }

    private static void LogProviderSelection(string providerName, string reason)
    {
        var message = $"[ConversationProviderFactory] Selected {providerName} ({reason})";
        Console.WriteLine(message);
        System.Diagnostics.Debug.WriteLine(message);
    }
}

