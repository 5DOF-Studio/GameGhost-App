using System.Text.Json;
using FluentAssertions;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Conversation;

public class OpenAiRealtimeSessionOptionsTests
{
    [Fact]
    public void BuildSessionUpdateJson_EnablesSemanticVadAutoResponse()
    {
        var json = OpenAiRealtimeSessionOptions.BuildSessionUpdateJson("test instructions", "ash");

        using var doc = JsonDocument.Parse(json);
        var session = doc.RootElement.GetProperty("session");
        var turnDetection = session.GetProperty("turn_detection");

        turnDetection.GetProperty("type").GetString().Should().Be("semantic_vad");
        turnDetection.GetProperty("eagerness").GetString().Should().Be("low");
        turnDetection.GetProperty("create_response").GetBoolean().Should().BeTrue();
        turnDetection.GetProperty("interrupt_response").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void BuildSessionUpdateJson_EnablesNearFieldNoiseReduction()
    {
        var json = OpenAiRealtimeSessionOptions.BuildSessionUpdateJson("test instructions", "ash");

        using var doc = JsonDocument.Parse(json);
        var noiseReduction = doc.RootElement
            .GetProperty("session")
            .GetProperty("input_audio_noise_reduction");

        noiseReduction.GetProperty("type").GetString().Should().Be("near_field");
    }
}
