using System.Net;
using WitnessDesktop.Services.Local;
using WitnessDesktop.Tests.Helpers;

namespace WitnessDesktop.Tests.Services;

public class OllamaLocalModelRuntimeTests
{
    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:11434")
        };
    }

    [Fact]
    public async Task GetHealthAsync_WithInstalledConfiguredModel_ReturnsBrainAvailable()
    {
        var httpClient = CreateHttpClient(MockHttpHandler.FromJson(
            """{"models":[{"name":"minicpm-v:latest","model":"minicpm-v:latest"}]}"""));
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v");

        var health = await sut.GetHealthAsync();

        health.RuntimeAvailable.Should().BeTrue();
        health.BrainAvailable.Should().BeTrue();
        health.VoiceAvailable.Should().BeFalse();
        health.RuntimeName.Should().Be("ollama");
        health.ModelId.Should().Be("minicpm-v");
        health.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task GetHealthAsync_WithMissingConfiguredModel_ReturnsUnavailableBrain()
    {
        var httpClient = CreateHttpClient(MockHttpHandler.FromJson(
            """{"models":[{"name":"llama3.2","model":"llama3.2"}]}"""));
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v");

        var health = await sut.GetHealthAsync();

        health.RuntimeAvailable.Should().BeTrue();
        health.BrainAvailable.Should().BeFalse();
        health.FailureReason.Should().Contain("not installed");
    }

    [Fact]
    public async Task GetHealthAsync_WithHttpFailure_ReturnsRuntimeUnavailable()
    {
        var httpClient = CreateHttpClient(MockHttpHandler.FromJson(
            """{"error":"unavailable"}""",
            HttpStatusCode.ServiceUnavailable));
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v");

        var health = await sut.GetHealthAsync();

        health.RuntimeAvailable.Should().BeFalse();
        health.BrainAvailable.Should().BeFalse();
        health.FailureReason.Should().Contain("503");
    }

    [Fact]
    public async Task GetHealthAsync_WithConnectionError_ReturnsRuntimeUnavailable()
    {
        var handler = new MockHttpHandler((_, _) => throw new HttpRequestException("connection refused"));
        var httpClient = CreateHttpClient(handler);
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v");

        var health = await sut.GetHealthAsync();

        health.RuntimeAvailable.Should().BeFalse();
        health.FailureReason.Should().Contain("connection refused");
    }

    [Fact]
    public async Task GetHealthAsync_VoiceEnabledTrue_ModelInstalled_ReturnsVoiceAvailable()
    {
        var httpClient = CreateHttpClient(MockHttpHandler.FromJson(
            """{"models":[{"name":"minicpm-v:latest","model":"minicpm-v:latest"}]}"""));
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v", voiceEnabled: true);

        var health = await sut.GetHealthAsync();

        health.VoiceAvailable.Should().BeTrue();
        health.BrainAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetHealthAsync_VoiceEnabledTrue_ModelNotInstalled_ReturnsVoiceUnavailable()
    {
        var httpClient = CreateHttpClient(MockHttpHandler.FromJson(
            """{"models":[{"name":"llama3.2","model":"llama3.2"}]}"""));
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v", voiceEnabled: true);

        var health = await sut.GetHealthAsync();

        health.VoiceAvailable.Should().BeFalse();
        health.BrainAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetHealthAsync_VoiceEnabledFalse_ModelInstalled_ReturnsVoiceUnavailable()
    {
        var httpClient = CreateHttpClient(MockHttpHandler.FromJson(
            """{"models":[{"name":"minicpm-v:latest","model":"minicpm-v:latest"}]}"""));
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v", voiceEnabled: false);

        var health = await sut.GetHealthAsync();

        health.VoiceAvailable.Should().BeFalse();
        health.BrainAvailable.Should().BeTrue();
    }

    // ─── Speech capability reporting ───

    [Fact]
    public async Task GetHealthAsync_WithSttAndTts_ReportsSpeechCapabilities()
    {
        var httpClient = CreateHttpClient(MockHttpHandler.FromJson(
            """{"models":[{"name":"minicpm-v:latest","model":"minicpm-v:latest"}]}"""));
        var stt = new FakeSpeechToTextProvider(true, "Apple Speech");
        var tts = new FakeTextToSpeechProvider(true, "Apple TTS");
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v", voiceEnabled: true, stt: stt, tts: tts);

        var health = await sut.GetHealthAsync();

        health.SpeechInputAvailable.Should().BeTrue();
        health.SpeechOutputAvailable.Should().BeTrue();
        health.SttEngineName.Should().Be("Apple Speech");
        health.TtsEngineName.Should().Be("Apple TTS");
    }

    [Fact]
    public async Task GetHealthAsync_WithoutSttTts_ReportsSpeechUnavailable()
    {
        var httpClient = CreateHttpClient(MockHttpHandler.FromJson(
            """{"models":[{"name":"minicpm-v:latest","model":"minicpm-v:latest"}]}"""));
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v");

        var health = await sut.GetHealthAsync();

        health.SpeechInputAvailable.Should().BeFalse();
        health.SpeechOutputAvailable.Should().BeFalse();
        health.SttEngineName.Should().BeNull();
        health.TtsEngineName.Should().BeNull();
    }

    [Fact]
    public async Task GetHealthAsync_SttOnlyNoTts_ReportsPartialSpeech()
    {
        var httpClient = CreateHttpClient(MockHttpHandler.FromJson(
            """{"models":[{"name":"minicpm-v:latest","model":"minicpm-v:latest"}]}"""));
        var stt = new FakeSpeechToTextProvider(true, "Apple Speech");
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v", stt: stt);

        var health = await sut.GetHealthAsync();

        health.SpeechInputAvailable.Should().BeTrue();
        health.SpeechOutputAvailable.Should().BeFalse();
        health.SttEngineName.Should().Be("Apple Speech");
        health.TtsEngineName.Should().BeNull();
    }

    [Fact]
    public async Task GetHealthAsync_TtsOnlyNoStt_ReportsPartialSpeech()
    {
        var httpClient = CreateHttpClient(MockHttpHandler.FromJson(
            """{"models":[{"name":"minicpm-v:latest","model":"minicpm-v:latest"}]}"""));
        var tts = new FakeTextToSpeechProvider(true, "Apple TTS");
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v", tts: tts);

        var health = await sut.GetHealthAsync();

        health.SpeechInputAvailable.Should().BeFalse();
        health.SpeechOutputAvailable.Should().BeTrue();
        health.TtsEngineName.Should().Be("Apple TTS");
        health.SttEngineName.Should().BeNull();
    }

    [Fact]
    public async Task GetHealthAsync_RuntimeUnavailable_StillReportsSpeechCapabilities()
    {
        var handler = new MockHttpHandler((_, _) => throw new HttpRequestException("connection refused"));
        var httpClient = CreateHttpClient(handler);
        var stt = new FakeSpeechToTextProvider(true, "Apple Speech");
        var tts = new FakeTextToSpeechProvider(true, "Apple TTS");
        var sut = new OllamaLocalModelRuntime(httpClient, "minicpm-v", stt: stt, tts: tts);

        var health = await sut.GetHealthAsync();

        health.RuntimeAvailable.Should().BeFalse();
        health.SpeechInputAvailable.Should().BeTrue();
        health.SpeechOutputAvailable.Should().BeTrue();
    }

    // ─── Fake speech providers for testing ───

    private sealed class FakeSpeechToTextProvider : ISpeechToTextProvider
    {
        private readonly bool _available;
        private readonly string _engineName;

        public FakeSpeechToTextProvider(bool available, string engineName)
        {
            _available = available;
            _engineName = engineName;
        }

        public bool IsAvailable => _available;
        public string EngineName => _engineName;
        public Task<string?> TranscribeAsync(byte[] pcmAudio, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeTextToSpeechProvider : ITextToSpeechProvider
    {
        private readonly bool _available;
        private readonly string _engineName;

        public FakeTextToSpeechProvider(bool available, string engineName)
        {
            _available = available;
            _engineName = engineName;
        }

        public bool IsAvailable => _available;
        public string EngineName => _engineName;
        public Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct = default)
            => Task.FromResult<byte[]?>(null);
    }
}
