using System.Text.Json;
using GaimerDesktop.Models;

namespace GaimerDesktop.Tests.Models;

public class ContentConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Read_StringContent_ReturnsString()
    {
        var json = """{"role":"user","content":"hello world"}""";

        var msg = JsonSerializer.Deserialize<OpenRouterMessage>(json, Options)!;

        msg.Content.Should().BeOfType<string>().Which.Should().Be("hello world");
    }

    [Fact]
    public void Read_ArrayContent_ReturnsListContentPart()
    {
        var json = """{"role":"user","content":[{"type":"text","text":"describe this"}]}""";

        var msg = JsonSerializer.Deserialize<OpenRouterMessage>(json, Options)!;

        msg.Content.Should().BeOfType<List<ContentPart>>();
        var parts = (List<ContentPart>)msg.Content!;
        parts.Should().HaveCount(1);
        parts[0].Type.Should().Be("text");
        parts[0].Text.Should().Be("describe this");
    }

    [Fact]
    public void Read_NullContent_ReturnsNull()
    {
        var json = """{"role":"assistant","content":null}""";

        var msg = JsonSerializer.Deserialize<OpenRouterMessage>(json, Options)!;

        msg.Content.Should().BeNull();
    }

    [Fact]
    public void Write_String_WritesJsonString()
    {
        var msg = new OpenRouterMessage { Role = "user", Content = "test message" };

        var json = JsonSerializer.Serialize(msg, Options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("content").GetString().Should().Be("test message");
    }

    [Fact]
    public void Write_ListContentPart_WritesJsonArray()
    {
        var parts = new List<ContentPart>
        {
            new() { Type = "text", Text = "what's in this image?" },
            new() { Type = "image_url", ImageUrl = new ImageUrl { Url = "data:image/png;base64,abc" } },
        };
        var msg = new OpenRouterMessage { Role = "user", Content = parts };

        var json = JsonSerializer.Serialize(msg, Options);

        using var doc = JsonDocument.Parse(json);
        var contentArray = doc.RootElement.GetProperty("content");
        contentArray.ValueKind.Should().Be(JsonValueKind.Array);
        contentArray.GetArrayLength().Should().Be(2);
    }
}
