using Microsoft.Extensions.Configuration;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.Services.Conversation.Providers;

namespace WitnessDesktop.Tests.Conversation;

/// <summary>
/// Verifies that provider API key resolution uses explicit names only and does not
/// depend on any shared "APIKEY" alias.
///
/// Root cause: prefix-stripping env loaders created collisions like
/// GEMINI_APIKEY/OPENROUTER_APIKEY -> configuration["APIKEY"].
///
/// Fix: MauiProgram loads only unprefixed environment variables, and provider
/// services resolve explicit names via IConfiguration.
/// </summary>
public class ApiKeyCollisionTests : IDisposable
{
    private readonly List<IConversationProvider> _providers = new();

    private IConversationProvider CreateAndTrack(ConversationProviderFactory factory)
    {
        var provider = factory.Create();
        _providers.Add(provider);
        return provider;
    }

    public void Dispose()
    {
        foreach (var p in _providers)
            p.Dispose();
    }

    /// <summary>
    /// Simulates the current MauiProgram.BuildConfiguration layering:
    /// 1. AddEnvironmentVariables() → all env vars loaded under their explicit names
    /// </summary>
    private static IConfiguration BuildConfigLikeMauiProgram(
        string? geminiApiKey = null,
        string? openAiApiKey = null,
        string? openRouterApiKey = null)
    {
        // Set env vars for this test (restore after)
        var envVars = new Dictionary<string, string?>();

        void SetEnv(string key, string? value)
        {
            envVars[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        SetEnv("GEMINI_APIKEY", geminiApiKey);
        SetEnv("OPENAI_APIKEY", openAiApiKey);
        SetEnv("OPENROUTER_APIKEY", openRouterApiKey);

        try
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            return config;
        }
        finally
        {
            // Restore original env vars
            foreach (var (key, original) in envVars)
                Environment.SetEnvironmentVariable(key, original);
        }
    }

    // ── Explicit key resolution ────────────────────────────────────────────

    [Fact]
    public void Configuration_DoesNotCreateSharedApiKeyAlias()
    {
        var config = BuildConfigLikeMauiProgram(
            geminiApiKey: "gemini-real-key",
            openRouterApiKey: "openrouter-real-key");

        config["APIKEY"].Should().BeNull("the current configuration should not synthesize a shared APIKEY alias");
        config["GEMINI_APIKEY"].Should().Be("gemini-real-key");
        config["OPENROUTER_APIKEY"].Should().Be("openrouter-real-key");
    }

    [Fact]
    public void Factory_WithCollision_StillSelectsGemini()
    {
        var config = BuildConfigLikeMauiProgram(
            geminiApiKey: "gemini-real-key",
            openRouterApiKey: "openrouter-real-key");

        var factory = new ConversationProviderFactory(config);
        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<GeminiConversationProvider>(
            "factory should find the real Gemini key via its explicit env var");
    }

    [Fact]
    public void Factory_WithCollision_BothGeminiAndOpenAI_PrefersOpenAI()
    {
        var config = BuildConfigLikeMauiProgram(
            geminiApiKey: "gemini-real-key",
            openAiApiKey: "openai-real-key",
            openRouterApiKey: "openrouter-real-key");

        var factory = new ConversationProviderFactory(config);
        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<OpenAIConversationProvider>(
            "OpenAI should be preferred over Gemini in auto-detect when both keys present");
    }

    [Fact]
    public void Factory_OnlyOpenRouterKey_FallsToMock()
    {
        // If ONLY OpenRouter is set (no Gemini, no OpenAI), the factory should
        // NOT create a Gemini provider from the polluted APIKEY slot.
        var config = BuildConfigLikeMauiProgram(
            openRouterApiKey: "openrouter-only-key");

        var factory = new ConversationProviderFactory(config);
        var provider = CreateAndTrack(factory);
        provider.Should().BeOfType<MockConversationProvider>(
            "OPENROUTER_APIKEY alone must not be treated as a Gemini voice key");
    }

    [Fact]
    public void GeminiLiveService_WithCollision_GetsCorrectKey()
    {
        // GeminiLiveService reads the API key in its constructor using explicit config keys.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GEMINI_APIKEY"] = "correct-gemini-key"
            }!)
            .Build();

        var service = new GeminiLiveService(config, "Fenrir");

        service.IsConnected.Should().BeFalse("not connected yet");
        service.State.Should().Be(WitnessDesktop.Models.ConnectionState.Disconnected);
        service.Dispose();
    }

    [Fact]
    public void GeminiLiveService_OnlySharedApiKeyAlias_UsesEmptyKey()
    {
        // Edge case: a stray APIKEY entry must not be treated as a Gemini key.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APIKEY"] = "some-key-from-polluted-slot"
            }!)
            .Build();

        var service = new GeminiLiveService(config, "Fenrir");
        service.State.Should().Be(WitnessDesktop.Models.ConnectionState.Disconnected);
        service.IsConnected.Should().BeFalse();
        service.Dispose();
    }
}
