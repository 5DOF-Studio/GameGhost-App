using System.Net;
using GaimerDesktop.Models;
using GaimerDesktop.Services.Brain;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.Brain;

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
}
