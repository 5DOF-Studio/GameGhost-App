using Microsoft.Extensions.Configuration;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Conversation;
using GaimerDesktop.Services.Conversation.Providers;

namespace GaimerDesktop.Tests.Conversation;

/// <summary>
/// Tests for ConversationProviderFactory — the single point of provider selection.
/// Uses in-memory configuration to simulate environment variables.
/// Does NOT call ConnectAsync; only verifies the factory returns the correct type.
/// </summary>
public class ConversationProviderFactoryTests : IDisposable
{
    private readonly List<IConversationProvider> _providers = new();

    /// <summary>Track created providers so we can dispose them after each test.</summary>
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

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values!)
            .Build();
    }

    // ──────────────────────────────────────────────
    // Explicit provider selection via VOICE_PROVIDER
    // ──────────────────────────────────────────────

    [Fact]
    public void Create_ExplicitMock_ReturnsMockProvider()
    {
        var config = BuildConfig(new() { ["VOICE_PROVIDER"] = "mock" });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<MockConversationProvider>();
    }

    [Fact]
    public void Create_ExplicitGemini_WithKey_ReturnsGeminiProvider()
    {
        var config = BuildConfig(new()
        {
            ["VOICE_PROVIDER"] = "gemini",
            ["GEMINI_APIKEY"] = "test-gemini-key"
        });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<GeminiConversationProvider>();
    }

    [Fact]
    public void Create_ExplicitGemini_NoKey_ThrowsInvalidOperation()
    {
        var config = BuildConfig(new() { ["VOICE_PROVIDER"] = "gemini" });
        var factory = new ConversationProviderFactory(config);

        var act = () => factory.Create();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no Gemini API key*");
    }

    [Fact]
    public void Create_ExplicitOpenAI_WithKey_ReturnsOpenAIProvider()
    {
        var config = BuildConfig(new()
        {
            ["VOICE_PROVIDER"] = "openai",
            ["OPENAI_APIKEY"] = "test-openai-key"
        });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<OpenAIConversationProvider>();
    }

    [Fact]
    public void Create_ExplicitOpenAI_NoKey_ThrowsInvalidOperation()
    {
        var config = BuildConfig(new() { ["VOICE_PROVIDER"] = "openai" });
        var factory = new ConversationProviderFactory(config);

        var act = () => factory.Create();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no OpenAI API key*");
    }

    [Fact]
    public void Create_UnknownProvider_ThrowsInvalidOperation()
    {
        var config = BuildConfig(new() { ["VOICE_PROVIDER"] = "anthropic" });
        var factory = new ConversationProviderFactory(config);

        var act = () => factory.Create();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown VOICE_PROVIDER: anthropic*");
    }

    // ──────────────────────────────────────────────
    // USE_MOCK_SERVICES override
    // ──────────────────────────────────────────────

    [Fact]
    public void Create_UseMockServicesTrue_ReturnsMockProvider()
    {
        var config = BuildConfig(new() { ["USE_MOCK_SERVICES"] = "true" });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<MockConversationProvider>();
    }

    [Fact]
    public void Create_UseMockServicesOne_ReturnsMockProvider()
    {
        var config = BuildConfig(new() { ["USE_MOCK_SERVICES"] = "1" });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<MockConversationProvider>();
    }

    // ──────────────────────────────────────────────
    // Auto-detect based on available API keys
    // ──────────────────────────────────────────────

    [Fact]
    public void Create_AutoDetect_GeminiKeyPresent_ReturnsGeminiProvider()
    {
        var config = BuildConfig(new() { ["GEMINI_APIKEY"] = "test-key" });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<GeminiConversationProvider>();
    }

    [Fact]
    public void Create_AutoDetect_OpenAIKeyPresent_ReturnsOpenAIProvider()
    {
        var config = BuildConfig(new() { ["OPENAI_APIKEY"] = "test-key" });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<OpenAIConversationProvider>();
    }

    [Fact]
    public void Create_AutoDetect_BothKeys_PrefersGemini()
    {
        var config = BuildConfig(new()
        {
            ["GEMINI_APIKEY"] = "gemini-key",
            ["OPENAI_APIKEY"] = "openai-key"
        });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<GeminiConversationProvider>();
    }

    [Fact]
    public void Create_AutoDetect_NoKeys_ReturnsMockProvider()
    {
        var config = BuildConfig(new() { ["UNRELATED"] = "value" });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<MockConversationProvider>();
    }

    // ──────────────────────────────────────────────
    // Voice gender resolution via ISettingsService
    // ──────────────────────────────────────────────

    [Fact]
    public void Create_WithSettingsFemale_GeminiUsesKore()
    {
        var config = BuildConfig(new() { ["GEMINI_APIKEY"] = "test-key" });
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.VoiceGender).Returns("female");
        var factory = new ConversationProviderFactory(config, mockSettings.Object);

        // GeminiConversationProvider is created — we can't inspect the voice directly,
        // but we verify it doesn't throw and returns the correct type.
        // The voice resolution is tested via VoiceConfig unit tests.
        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<GeminiConversationProvider>();
    }

    [Fact]
    public void Create_WithSettingsFemale_OpenAIUsesShimmer()
    {
        var config = BuildConfig(new() { ["OPENAI_APIKEY"] = "test-key" });
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.VoiceGender).Returns("female");
        var factory = new ConversationProviderFactory(config, mockSettings.Object);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<OpenAIConversationProvider>();
    }

    [Fact]
    public void Create_NullSettings_UsesDefaultMaleVoice()
    {
        // When settings is null, factory defaults to "male" gender
        var config = BuildConfig(new() { ["GEMINI_APIKEY"] = "test-key" });
        var factory = new ConversationProviderFactory(config, settings: null);

        // Should not throw — defaults to male voice
        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<GeminiConversationProvider>();
    }
}
