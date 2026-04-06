using System.Net;
using WitnessDesktop.Models;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Tests.Helpers;

namespace WitnessDesktop.Tests.Brain;

public class OpenRouterClientTests
{
    private OpenRouterClient CreateClient(MockHttpHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new OpenRouterClient(httpClient, "test-key", "test-model");
    }

    private static OpenRouterRequest SimpleRequest() => new()
    {
        Model = "test-model",
        Messages = new List<OpenRouterMessage>
        {
            new() { Role = "user", Content = "Hello" }
        },
        MaxTokens = 10,
        Stream = false
    };

    private const string ValidResponse =
        """{"id":"gen-123","choices":[{"index":0,"message":{"role":"assistant","content":"Hi"},"finish_reason":"stop"}],"usage":{"prompt_tokens":5,"completion_tokens":2,"total_tokens":7}}""";

    // ── ChatCompletionAsync Happy Path ───────────────────────────────────────

    [Fact]
    public async Task ChatCompletionAsync_Success_ReturnsDeserializedResponse()
    {
        var client = CreateClient(MockHttpHandler.FromJson(ValidResponse));

        var response = await client.ChatCompletionAsync(SimpleRequest());

        response.Should().NotBeNull();
        response.Choices.Should().HaveCount(1);
        response.Choices[0].Message.Content.Should().Be("Hi");
        response.Choices[0].FinishReason.Should().Be("stop");
    }

    // ── HTTP Error Status Codes ─────────────────────────────────────────────

    [Fact]
    public async Task ChatCompletionAsync_401Unauthorized_ThrowsOpenRouterException()
    {
        var client = CreateClient(MockHttpHandler.FromJson(
            """{"error":{"message":"Invalid API key","code":401}}""",
            HttpStatusCode.Unauthorized));

        var act = () => client.ChatCompletionAsync(SimpleRequest());

        var ex = await act.Should().ThrowAsync<OpenRouterException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ex.Which.ResponseBody.Should().Contain("Invalid API key");
    }

    [Fact]
    public async Task ChatCompletionAsync_429RateLimited_ThrowsOpenRouterException()
    {
        var client = CreateClient(MockHttpHandler.FromJson(
            """{"error":{"message":"Rate limit exceeded","code":429}}""",
            HttpStatusCode.TooManyRequests));

        var act = () => client.ChatCompletionAsync(SimpleRequest());

        var ex = await act.Should().ThrowAsync<OpenRouterException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        ex.Which.ResponseBody.Should().Contain("Rate limit");
    }

    [Fact]
    public async Task ChatCompletionAsync_500ServerError_ThrowsOpenRouterException()
    {
        var client = CreateClient(MockHttpHandler.FromJson(
            """{"error":{"message":"Internal server error"}}""",
            HttpStatusCode.InternalServerError));

        var act = () => client.ChatCompletionAsync(SimpleRequest());

        var ex = await act.Should().ThrowAsync<OpenRouterException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── Malformed / Null Responses ──────────────────────────────────────────

    [Fact]
    public async Task ChatCompletionAsync_MalformedJson_ThrowsJsonException()
    {
        var client = CreateClient(MockHttpHandler.FromJson("not valid json at all {{{"));

        var act = () => client.ChatCompletionAsync(SimpleRequest());

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task ChatCompletionAsync_NullDeserialization_ThrowsOpenRouterException()
    {
        // Valid JSON but deserializes to null (empty object with wrong shape)
        var client = CreateClient(MockHttpHandler.FromJson("null"));

        var act = () => client.ChatCompletionAsync(SimpleRequest());

        // Could throw JsonException or OpenRouterException depending on deserializer behavior
        await act.Should().ThrowAsync<Exception>();
    }

    // ── Cancellation ────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatCompletionAsync_Cancelled_ThrowsOperationCanceled()
    {
        var handler = new MockHttpHandler(async (_, ct) =>
        {
            await Task.Delay(5000, ct); // simulate slow response
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(handler);
        using var cts = new CancellationTokenSource(50);

        var act = () => client.ChatCompletionAsync(SimpleRequest(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── OpenRouterException Validation ───────────────────────────────────────

    [Fact]
    public void OpenRouterException_CarriesStatusCodeAndBody()
    {
        var ex = new OpenRouterException(HttpStatusCode.Forbidden, "Access denied");

        ex.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ex.ResponseBody.Should().Be("Access denied");
        ex.Message.Should().Contain("403");
        ex.Message.Should().Contain("Access denied");
    }

    // ── Request Builder Helpers ─────────────────────────────────────────────

    [Fact]
    public void CreateImageAnalysisRequest_BuildsVisionRequest()
    {
        var client = CreateClient(MockHttpHandler.FromJson(ValidResponse));
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header

        var request = client.CreateImageAnalysisRequest(imageBytes, "Analyze this chess board");

        request.Model.Should().Be("test-model");
        request.Messages.Should().HaveCount(1);
        request.Messages[0].Role.Should().Be("user");
        request.MaxTokens.Should().Be(1024);

        // Content should be a list of ContentPart (multimodal)
        var parts = request.Messages[0].Content as List<ContentPart>;
        parts.Should().NotBeNull();
        parts.Should().HaveCount(2);
        parts![0].Type.Should().Be("text");
        parts[0].Text.Should().Contain("Analyze this chess board");
        parts[1].Type.Should().Be("image_url");
        parts[1].ImageUrl!.Url.Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public void CreateImageAnalysisRequest_WithModelOverride_UsesOverride()
    {
        var client = CreateClient(MockHttpHandler.FromJson(ValidResponse));

        var request = client.CreateImageAnalysisRequest(
            new byte[] { 1, 2, 3 }, "prompt", "custom-model");

        request.Model.Should().Be("custom-model");
    }

    [Fact]
    public void CreateToolCallRequest_BuildsRequestWithTools()
    {
        var client = CreateClient(MockHttpHandler.FromJson(ValidResponse));
        var messages = new List<OpenRouterMessage>
        {
            new() { Role = "user", Content = "What's the best move?" }
        };
        var tools = new List<OpenRouterTool>
        {
            new()
            {
                Type = "function",
                Function = new OpenRouterFunction
                {
                    Name = "get_game_state",
                    Description = "Get current game state"
                }
            }
        };

        var request = client.CreateToolCallRequest(messages, tools);

        request.Model.Should().Be("test-model");
        request.Messages.Should().BeSameAs(messages);
        request.Tools.Should().BeSameAs(tools);
        request.ToolChoice.Should().Be("auto");
    }

    // ── Provider Preferences ──────────────────────────────────────────────────

    [Fact]
    public void CreateImageAnalysisRequest_ForGeminiModel_IncludesProviderPreferences()
    {
        var client = CreateClient(MockHttpHandler.FromJson(ValidResponse));
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header

        var request = client.CreateImageAnalysisRequest(
            imageBytes, "Analyze board", "google/gemini-2.5-flash");

        request.Provider.Should().NotBeNull();
        var providerJson = request.Provider!.Value.GetRawText();
        providerJson.Should().Contain("\"Google\"");
        providerJson.Should().Contain("\"allow_fallbacks\":false");
    }

    [Fact]
    public void CreateImageAnalysisRequest_ForNonGeminiModel_NoProviderPreferences()
    {
        var client = CreateClient(MockHttpHandler.FromJson(ValidResponse));
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var request = client.CreateImageAnalysisRequest(
            imageBytes, "Analyze board", "anthropic/claude-sonnet-4");

        request.Provider.Should().BeNull();
    }

    [Fact]
    public void CreateImageAnalysisRequest_ForGeminiDefaultModel_IncludesProviderPreferences()
    {
        // Client created with Gemini as default model
        var httpClient = new HttpClient(MockHttpHandler.FromJson(ValidResponse));
        var client = new OpenRouterClient(httpClient, "test-key", "google/gemini-2.5-flash");
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var request = client.CreateImageAnalysisRequest(imageBytes, "Analyze board");

        request.Provider.Should().NotBeNull();
        var providerJson = request.Provider!.Value.GetRawText();
        providerJson.Should().Contain("\"allow_fallbacks\":false");
    }

    // ── Vision Request Round-Trip ────────────────────────────────────────────

    [Fact]
    public async Task ChatCompletionAsync_WithImageRequest_SerializesAndDeserializes()
    {
        var client = CreateClient(MockHttpHandler.FromJson(ValidResponse));
        var request = client.CreateImageAnalysisRequest(
            new byte[] { 0x89, 0x50 }, "Analyze this", "test-model");

        var response = await client.ChatCompletionAsync(request);

        response.Should().NotBeNull();
        response.Choices.Should().HaveCount(1);
        response.Choices[0].Message.Content.Should().Be("Hi");
    }

    // ── Streaming Error ─────────────────────────────────────────────────────

    [Fact]
    public async Task ChatCompletionStreamAsync_ErrorStatus_ThrowsOpenRouterException()
    {
        var client = CreateClient(MockHttpHandler.FromJson(
            """{"error":"rate limited"}""",
            HttpStatusCode.TooManyRequests));

        var act = async () =>
        {
            await foreach (var chunk in client.ChatCompletionStreamAsync(SimpleRequest()))
            {
                // Should not reach here
            }
        };

        await act.Should().ThrowAsync<OpenRouterException>();
    }

    // ── Dynamic Schema Generation ────────────────────────────────────────────

    [Fact]
    public void CreateImageAnalysisRequest_WithChessPack_SchemaMatchesLegacy()
    {
        // Build schema from chess pack fields (same as pack.json)
        var schema = new ObservationSchema
        {
            SchemaName = "chess_analysis",
            Fields = new List<ObservationField>
            {
                new() { Key = "visual_description", Type = "string", Required = true, Description = "Literal description of what you see on the board" },
                new() { Key = "position_assessment", Type = "string", Required = true, Description = "Who is better and why" },
                new() { Key = "threats", Type = "string?", Required = false, Description = "Key threat or opportunity" },
                new() { Key = "suggested_action", Type = "string?", Required = false, Description = "Recommended action" },
                new() { Key = "fen", Type = "string?", Required = false, Description = "FEN string or null if UNREADABLE" },
                new() { Key = "last_move", Type = "string?", Required = false, Description = "The move just played, or null if unclear" },
                new() { Key = "confidence", Type = "enum", Required = true, Description = "Overall confidence level",
                         Values = new() { "CERTAIN", "LIKELY", "UNCERTAIN", "GUESSING" } },
            }
        };

        var result = schema.BuildResponseFormat();
        var jsonSchema = result.GetProperty("json_schema").GetProperty("schema");

        // Verify all 7 properties exist
        var props = jsonSchema.GetProperty("properties");
        Assert.True(props.TryGetProperty("visual_description", out _));
        Assert.True(props.TryGetProperty("position_assessment", out _));
        Assert.True(props.TryGetProperty("threats", out _));
        Assert.True(props.TryGetProperty("suggested_action", out _));
        Assert.True(props.TryGetProperty("fen", out _));
        Assert.True(props.TryGetProperty("last_move", out _));
        Assert.True(props.TryGetProperty("confidence", out _));

        // Verify required matches old schema
        var required = jsonSchema.GetProperty("required");
        var reqList = new List<string>();
        foreach (var r in required.EnumerateArray()) reqList.Add(r.GetString()!);
        Assert.Equal(3, reqList.Count);
        Assert.Contains("visual_description", reqList);
        Assert.Contains("position_assessment", reqList);
        Assert.Contains("confidence", reqList);

        // Verify strict mode
        Assert.True(result.GetProperty("json_schema").GetProperty("strict").GetBoolean());
    }
}
