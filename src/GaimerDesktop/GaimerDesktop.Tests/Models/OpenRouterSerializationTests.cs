using System.Text.Json;
using GaimerDesktop.Models;

namespace GaimerDesktop.Tests.Models;

public class OpenRouterSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void RoundTrip_OpenRouterRequest_PreservesData()
    {
        var request = new OpenRouterRequest
        {
            Model = "openai/gpt-4o-mini",
            Messages = new List<OpenRouterMessage>
            {
                new() { Role = "user", Content = "Hello" }
            },
            MaxTokens = 512,
            Temperature = 0.7,
            Stream = false
        };

        var json = JsonSerializer.Serialize(request, Options);
        var roundTripped = JsonSerializer.Deserialize<OpenRouterRequest>(json, Options)!;

        roundTripped.Model.Should().Be("openai/gpt-4o-mini");
        roundTripped.Messages.Should().HaveCount(1);
        roundTripped.MaxTokens.Should().Be(512);
        roundTripped.Temperature.Should().Be(0.7);

        // Verify snake_case naming
        json.Should().Contain("\"model\"");
        json.Should().Contain("\"max_tokens\"");
    }

    [Fact]
    public void RoundTrip_OpenRouterResponse_PreservesData()
    {
        var response = new OpenRouterResponse
        {
            Id = "gen-abc123",
            Choices = new List<Choice>
            {
                new()
                {
                    Index = 0,
                    Message = new OpenRouterMessage { Role = "assistant", Content = "Hi there" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage
            {
                PromptTokens = 10,
                CompletionTokens = 5,
                TotalTokens = 15
            }
        };

        var json = JsonSerializer.Serialize(response, Options);
        var roundTripped = JsonSerializer.Deserialize<OpenRouterResponse>(json, Options)!;

        roundTripped.Id.Should().Be("gen-abc123");
        roundTripped.Choices.Should().HaveCount(1);
        roundTripped.Choices[0].FinishReason.Should().Be("stop");
        roundTripped.Usage!.TotalTokens.Should().Be(15);
    }

    [Fact]
    public void Serialization_NullsOmitted()
    {
        var request = new OpenRouterRequest
        {
            Model = "test-model",
            Messages = new List<OpenRouterMessage>
            {
                new() { Role = "user", Content = "Hi" }
            },
            // Tools, ToolChoice, MaxTokens, Temperature are null
            Stream = false
        };

        var json = JsonSerializer.Serialize(request, Options);

        json.Should().NotContain("\"tools\"");
        json.Should().NotContain("\"tool_choice\"");
        json.Should().NotContain("\"max_tokens\"");
        json.Should().NotContain("\"temperature\"");
    }
}
