using System.Text.Json;
using FluentAssertions;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Conversation;

public class OpenAiRealtimeSessionOptionsTests
{
    [Fact]
    public void BuildSessionUpdateJson_EnablesServerVadAutoResponse()
    {
        var json = OpenAiRealtimeSessionOptions.BuildSessionUpdateJson("test instructions", "ash");

        using var doc = JsonDocument.Parse(json);
        var turnDetection = doc.RootElement
            .GetProperty("session")
            .GetProperty("turn_detection");

        turnDetection.GetProperty("type").GetString().Should().Be("server_vad");
        turnDetection.GetProperty("create_response").GetBoolean().Should().BeTrue();
        turnDetection.GetProperty("interrupt_response").GetBoolean().Should().BeTrue();
    }
}
