using System.Net;
using System.Text.Json;
using WitnessDesktop.Services.Local;
using WitnessDesktop.Tests.Helpers;

namespace WitnessDesktop.Tests.Conversation;

public class OllamaTextConversationBackendTests
{
    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:11434")
        };
    }

    [Fact]
    public async Task SendAsync_ReturnsResponseText()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(async (request, _) =>
        {
            capturedRequest = request;
            return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"message":{"content":"Hello from local!"}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
        });

        var sut = new OllamaTextConversationBackend(CreateHttpClient(handler), "minicpm-v");
        var history = new List<ConversationMessage>
        {
            new("system", "You are a gaming companion."),
            new("user", "What's up?")
        };

        var result = await sut.SendAsync(history);

        result.Should().Be("Hello from local!");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().EndWith("/api/chat");

        var json = await capturedRequest.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("model").GetString().Should().Be("minicpm-v");
        doc.RootElement.GetProperty("stream").GetBoolean().Should().BeFalse();

        var messages = doc.RootElement.GetProperty("messages");
        messages.GetArrayLength().Should().Be(2);
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[0].GetProperty("content").GetString().Should().Be("You are a gaming companion.");
        messages[1].GetProperty("role").GetString().Should().Be("user");
        messages[1].GetProperty("content").GetString().Should().Be("What's up?");
    }

    [Fact]
    public async Task SendAsync_ThrowsOnHttpFailure()
    {
        var handler = MockHttpHandler.FromJson("""{"error":"model not found"}""", HttpStatusCode.NotFound);
        var sut = new OllamaTextConversationBackend(CreateHttpClient(handler), "minicpm-v");

        var act = () => sut.SendAsync(new List<ConversationMessage> { new("user", "hi") });

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendAsync_ThrowsOnEmptyResponse()
    {
        var handler = MockHttpHandler.FromJson("""{"message":{"content":""}}""");
        var sut = new OllamaTextConversationBackend(CreateHttpClient(handler), "minicpm-v");

        var act = () => sut.SendAsync(new List<ConversationMessage> { new("user", "hi") });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void RuntimeName_IsOllama()
    {
        var handler = MockHttpHandler.FromJson("{}");
        var sut = new OllamaTextConversationBackend(CreateHttpClient(handler), "minicpm-v");

        sut.RuntimeName.Should().Be("ollama");
    }
}
