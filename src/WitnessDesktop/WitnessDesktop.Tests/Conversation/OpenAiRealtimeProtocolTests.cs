using System.Text.Json;
using FluentAssertions;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Conversation;

public class OpenAiRealtimeProtocolTests
{
    [Fact]
    public void BuildConversationItemCreateJson_UsesInputTextPayload()
    {
        var json = OpenAiRealtimeProtocol.BuildConversationItemCreateJson("  hello coach  ");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("type").GetString().Should().Be("conversation.item.create");
        var item = root.GetProperty("item");
        item.GetProperty("type").GetString().Should().Be("message");
        item.GetProperty("role").GetString().Should().Be("user");
        item.GetProperty("status").GetString().Should().Be("completed");
        item.GetProperty("content")[0].GetProperty("type").GetString().Should().Be("input_text");
        item.GetProperty("content")[0].GetProperty("text").GetString().Should().Be("hello coach");
    }

    [Fact]
    public void BuildResponseCreateJson_RequestsAudioAndText()
    {
        var json = OpenAiRealtimeProtocol.BuildResponseCreateJson();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("type").GetString().Should().Be("response.create");
        var modalities = root.GetProperty("response").GetProperty("modalities").EnumerateArray().Select(x => x.GetString()).ToArray();
        modalities.Should().BeEquivalentTo(new[] { "text", "audio" });
    }
}
