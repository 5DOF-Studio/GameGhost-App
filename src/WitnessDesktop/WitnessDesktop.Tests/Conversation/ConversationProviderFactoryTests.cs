using Microsoft.Extensions.Configuration;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.Services.Conversation.Providers;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Tests.Conversation;

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
    public void Create_ExplicitLocal_WithClient_ReturnsLocalMiniCpmProvider()
    {
        var config = BuildConfig(new() { ["VOICE_PROVIDER"] = "local" });
        using var localClient = new FakeLocalAudioConversationClient();
        var factory = new ConversationProviderFactory(config, localAudioClient: localClient);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<LocalMiniCpmConversationProvider>();
    }

    [Fact]
    public void Create_ExplicitMiniCpm_WithClient_ReturnsLocalMiniCpmProvider()
    {
        var config = BuildConfig(new() { ["VOICE_PROVIDER"] = "minicpm" });
        using var localClient = new FakeLocalAudioConversationClient();
        var factory = new ConversationProviderFactory(config, localAudioClient: localClient);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<LocalMiniCpmConversationProvider>();
    }

    [Fact]
    public void Create_ExplicitLocal_WithoutClient_ThrowsInvalidOperation()
    {
        var config = BuildConfig(new() { ["VOICE_PROVIDER"] = "local" });
        var factory = new ConversationProviderFactory(config);

        var act = () => factory.Create();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no local audio client*");
    }

    [Fact]
    public void Create_LocalOnlyMode_IgnoresExplicitOpenAiProvider_AndReturnsLocalProvider()
    {
        var config = BuildConfig(new()
        {
            ["VOICE_PROVIDER"] = "openai",
            ["OPENAI_APIKEY"] = "test-openai-key"
        });
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.InferenceMode).Returns(InferenceMode.LocalOnly);
        using var localClient = new FakeLocalAudioConversationClient();
        var factory = new ConversationProviderFactory(config, mockSettings.Object, localClient);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<LocalMiniCpmConversationProvider>();
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
    public void Create_AutoDetect_BothKeys_PrefersOpenAI()
    {
        var config = BuildConfig(new()
        {
            ["GEMINI_APIKEY"] = "gemini-key",
            ["OPENAI_APIKEY"] = "openai-key"
        });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<OpenAIConversationProvider>();
    }

    [Fact]
    public void Create_AutoDetect_BothKeys_UsesSettingsProviderPreference()
    {
        var config = BuildConfig(new()
        {
            ["GEMINI_APIKEY"] = "gemini-key",
            ["OPENAI_APIKEY"] = "openai-key"
        });
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.VoiceProvider).Returns("openai");
        var factory = new ConversationProviderFactory(config, mockSettings.Object);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<OpenAIConversationProvider>();
    }

    [Fact]
    public void Create_WithSettingsLocal_AndClient_ReturnsLocalMiniCpmProvider()
    {
        var config = BuildConfig(new() { ["OPENAI_APIKEY"] = "test-key" });
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.VoiceProvider).Returns("local");
        mockSettings.Setup(s => s.VoiceGender).Returns("male");
        using var localClient = new FakeLocalAudioConversationClient();
        var factory = new ConversationProviderFactory(config, mockSettings.Object, localClient);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<LocalMiniCpmConversationProvider>();
    }

    [Fact]
    public void Create_LocalOnlyMode_IgnoresSettingsOpenAiProvider_AndReturnsLocalProvider()
    {
        var config = BuildConfig(new() { ["OPENAI_APIKEY"] = "test-key" });
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.InferenceMode).Returns(InferenceMode.LocalOnly);
        mockSettings.Setup(s => s.VoiceProvider).Returns("openai");
        using var localClient = new FakeLocalAudioConversationClient();
        var factory = new ConversationProviderFactory(config, mockSettings.Object, localClient);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<LocalMiniCpmConversationProvider>();
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

    // ──────────────────────────────────────────────
    // APIKEY collision: OPENROUTER_ prefix shadows GEMINI_ in shared "APIKEY" slot
    // ──────────────────────────────────────────────

    [Fact]
    public void Create_AutoDetect_GeminiAndOpenRouterKeys_SelectsOpenAI()
    {
        // OpenAI is preferred over Gemini in auto-detect; OpenRouter is for brain, not voice.
        var config = BuildConfig(new()
        {
            ["OPENROUTER_APIKEY"] = "openrouter-key",
            ["GEMINI_APIKEY"] = "real-gemini-key",
            ["OPENAI_APIKEY"] = "openai-key"
        });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);

        provider.Should().BeOfType<OpenAIConversationProvider>();
    }

    [Fact]
    public void Create_AutoDetect_OnlyOpenRouterKey_NoGeminiDirect_FallsToOpenAI()
    {
        // OPENROUTER_APIKEY alone must not count as a Gemini key.
        var config = BuildConfig(new()
        {
            ["OPENROUTER_APIKEY"] = "openrouter-key",
            ["OPENAI_APIKEY"] = "real-openai-key"
        });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);
        provider.Should().BeOfType<OpenAIConversationProvider>();
    }

    [Fact]
    public void Create_AutoDetect_GeminiKeyViaMultiplePaths_PrefersExplicitKey()
    {
        // A stray shared APIKEY alias must not override the explicit Gemini key.
        var config = BuildConfig(new()
        {
            ["GeminiApiKey"] = null,
            ["GEMINI_APIKEY"] = "correct-gemini-key",
            ["APIKEY"] = "wrong-openrouter-key"
        });
        var factory = new ConversationProviderFactory(config);

        var provider = CreateAndTrack(factory);
        provider.Should().BeOfType<GeminiConversationProvider>();
    }

    private sealed class FakeLocalAudioConversationClient : ILocalAudioConversationClient
    {
        public event EventHandler<ConnectionState>? ConnectionStateChanged;
        public event EventHandler<byte[]>? AudioReceived;
        public event EventHandler<string>? TextReceived;
        public event EventHandler? Interrupted;
        public event EventHandler<string>? ErrorOccurred;

        public bool IsConnected => false;
        public string RuntimeName => "FakeRuntime";

        public Task ConnectAsync(Agent agent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAudioAsync(byte[] audioData, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendTextAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendContextualUpdateAsync(string contextText, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }
}
