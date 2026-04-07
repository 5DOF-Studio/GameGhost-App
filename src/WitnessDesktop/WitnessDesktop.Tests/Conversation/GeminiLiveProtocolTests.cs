using System.Text.Json;
using FluentAssertions;
using WitnessDesktop.Services;
using Xunit;

namespace WitnessDesktop.Tests.Conversation;

public class GeminiLiveProtocolTests
{
    [Fact]
    public void BuildSetupMessageJson_UsesSupportedModelAndCamelCaseSchema()
    {
        var json = GeminiLiveProtocol.BuildSetupMessageJson("Stay brief.", "Fenrir");

        using var doc = JsonDocument.Parse(json);
        var setup = doc.RootElement.GetProperty("setup");

        setup.GetProperty("model").GetString().Should().Be("models/gemini-3.1-flash-live-preview");
        setup.GetProperty("generationConfig")
            .GetProperty("responseModalities")[0]
            .GetString()
            .Should().Be("AUDIO");
        setup.GetProperty("generationConfig")
            .GetProperty("speechConfig")
            .GetProperty("voiceConfig")
            .GetProperty("prebuiltVoiceConfig")
            .GetProperty("voiceName")
            .GetString()
            .Should().Be("Fenrir");
        setup.GetProperty("systemInstruction")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()
            .Should().Be("Stay brief.");

        json.Should().NotContain("generation_config");
        json.Should().NotContain("response_modalities");
        json.Should().NotContain("system_instruction");
    }

    [Fact]
    public void BuildAudioMessageJson_UsesRealtimeInputAudio()
    {
        var payload = new byte[] { 1, 2, 3, 4 };

        var json = GeminiLiveProtocol.BuildAudioMessageJson(payload);

        using var doc = JsonDocument.Parse(json);
        var audio = doc.RootElement.GetProperty("realtimeInput").GetProperty("audio");

        audio.GetProperty("mimeType").GetString().Should().Be("audio/pcm;rate=16000");
        audio.GetProperty("data").GetString().Should().Be(Convert.ToBase64String(payload));
        json.Should().NotContain("media_chunks");
        json.Should().NotContain("mime_type");
    }

    [Fact]
    public void BuildTextMessageJson_UsesRealtimeInputText()
    {
        var json = GeminiLiveProtocol.BuildTextMessageJson("  hello board  ");

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("realtimeInput").GetProperty("text").GetString().Should().Be("hello board");
        json.Should().NotContain("media_chunks");
    }

    [Fact]
    public void BuildImageMessageJson_UsesRealtimeInputVideoBlob()
    {
        var payload = new byte[] { 8, 9, 10 };

        var json = GeminiLiveProtocol.BuildImageMessageJson(payload, "image/jpeg");

        using var doc = JsonDocument.Parse(json);
        var video = doc.RootElement.GetProperty("realtimeInput").GetProperty("video");

        video.GetProperty("mimeType").GetString().Should().Be("image/jpeg");
        video.GetProperty("data").GetString().Should().Be(Convert.ToBase64String(payload));
    }

    [Fact]
    public void BuildSetupMessageJson_IncludesTranscriptionConfig()
    {
        var json = GeminiLiveProtocol.BuildSetupMessageJson("Brief.", "Fenrir");

        using var doc = JsonDocument.Parse(json);
        var setup = doc.RootElement.GetProperty("setup");

        setup.TryGetProperty("inputAudioTranscription", out _).Should().BeTrue();
        setup.TryGetProperty("outputAudioTranscription", out _).Should().BeTrue();
    }

    [Fact]
    public void ClassifyServerMessage_InputTranscription_IsServerContent()
    {
        var json = """{"serverContent":{"inputTranscription":{"text":"Hey Leroy what should I play"}}}""";

        GeminiLiveProtocol.ClassifyServerMessage(json).Should().Be(GeminiServerMessageKind.ServerContent);
    }

    [Theory]
    [InlineData("{\"setupComplete\":{}}", "SetupComplete")]
    [InlineData("{\"serverContent\":{\"turnComplete\":true}}", "ServerContent")]
    [InlineData("{\"goAway\":{}}", "GoAway")]
    [InlineData("{\"sessionResumptionUpdate\":{}}", "SessionResumptionUpdate")]
    public void ClassifyServerMessage_ReturnsExpectedKind(string json, string expected)
    {
        GeminiLiveProtocol.ClassifyServerMessage(json).ToString().Should().Be(expected);
    }

    [Fact]
    public void BuildSetupMessageJson_IncludesSessionResumptionAndCompression()
    {
        var json = GeminiLiveProtocol.BuildSetupMessageJson("Brief.", "Fenrir");

        using var doc = JsonDocument.Parse(json);
        var setup = doc.RootElement.GetProperty("setup");

        setup.TryGetProperty("sessionResumption", out _).Should().BeTrue();
        setup.TryGetProperty("contextWindowCompression", out _).Should().BeTrue();

        var compression = setup.GetProperty("contextWindowCompression");
        compression.TryGetProperty("slidingWindow", out _).Should().BeTrue();
    }

    [Fact]
    public void BuildSetupMessageJson_WithResumptionHandle_IncludesHandle()
    {
        var json = GeminiLiveProtocol.BuildSetupMessageJson("Brief.", "Fenrir",
            resumptionHandle: "abc123");

        using var doc = JsonDocument.Parse(json);
        var resumption = doc.RootElement.GetProperty("setup").GetProperty("sessionResumption");

        resumption.GetProperty("handle").GetString().Should().Be("abc123");
    }

    [Fact]
    public void BuildSetupMessageJson_IncludesVadConfig()
    {
        var json = GeminiLiveProtocol.BuildSetupMessageJson("Brief.", "Fenrir");

        using var doc = JsonDocument.Parse(json);
        var setup = doc.RootElement.GetProperty("setup");

        setup.TryGetProperty("realtimeInputConfig", out var inputConfig).Should().BeTrue();
        var vad = inputConfig.GetProperty("automaticActivityDetection");
        vad.TryGetProperty("disabled", out var disabled).Should().BeTrue();
        disabled.GetBoolean().Should().BeFalse();
    }
}
