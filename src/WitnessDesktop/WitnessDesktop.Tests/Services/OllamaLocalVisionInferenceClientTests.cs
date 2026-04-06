using System.Net;
using System.Text.Json;
using WitnessDesktop.Services.Local;
using WitnessDesktop.Tests.Helpers;

namespace WitnessDesktop.Tests.Services;

public class OllamaLocalVisionInferenceClientTests
{
    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:11434")
        };
    }

    [Fact]
    public async Task AnalyzeImageAsync_SendsSystemUserAndBase64Image_AndReturnsParsedResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(async (request, _) =>
        {
            capturedRequest = request;
            return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"model":"minicpm-v","message":{"content":"Board is stable."},"total_duration":250000000}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
        });

        var httpClient = CreateHttpClient(handler);
        var sut = new OllamaLocalVisionInferenceClient(httpClient, "minicpm-v");
        var request = new LocalVisionRequest
        {
            ImageData = [0x01, 0x02, 0x03],
            UserPrompt = "Analyze this frame.",
            SystemPrompt = "You are Gaimer.",
            CorrelationId = "corr-1"
        };

        var response = await sut.AnalyzeImageAsync(request);

        response.AssistantText.Should().Be("Board is stable.");
        response.ModelId.Should().Be("minicpm-v");
        response.LatencyMs.Should().Be(250);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().EndWith("/api/chat");

        var json = await capturedRequest.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("model").GetString().Should().Be("minicpm-v");
        doc.RootElement.GetProperty("stream").GetBoolean().Should().BeFalse();

        var messages = doc.RootElement.GetProperty("messages");
        messages.GetArrayLength().Should().Be(2);
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[0].GetProperty("content").GetString().Should().Be("You are Gaimer.");
        messages[1].GetProperty("role").GetString().Should().Be("user");
        messages[1].GetProperty("content").GetString().Should().Be("Analyze this frame.");
        messages[1].GetProperty("images")[0].GetString().Should().Be(Convert.ToBase64String(request.ImageData));
    }

    [Fact]
    public async Task AnalyzeImageAsync_WithModelOverride_UsesOverrideModel()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(async (request, _) =>
        {
            capturedRequest = request;
            return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"model":"custom-minicpm","message":{"content":"ok"}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
        });

        var sut = new OllamaLocalVisionInferenceClient(CreateHttpClient(handler), "minicpm-v");

        await sut.AnalyzeImageAsync(new LocalVisionRequest
        {
            ImageData = [0x0A],
            UserPrompt = "prompt",
            SystemPrompt = "system",
            ModelId = "custom-minicpm"
        });

        var json = await capturedRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("model").GetString().Should().Be("custom-minicpm");
    }

    [Fact]
    public async Task ChatAsync_SendsSystemAndUserMessages_WithoutImages()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(async (request, _) =>
        {
            capturedRequest = request;
            return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"message":{"content":"Local reply"}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
        });

        var sut = new OllamaLocalVisionInferenceClient(CreateHttpClient(handler), "minicpm-v");

        var reply = await sut.ChatAsync("User text", "System text");

        reply.Should().Be("Local reply");

        var json = await capturedRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var messages = doc.RootElement.GetProperty("messages");
        messages.GetArrayLength().Should().Be(2);
        messages[0].GetProperty("content").GetString().Should().Be("System text");
        messages[1].GetProperty("content").GetString().Should().Be("User text");
        messages[1].TryGetProperty("images", out _).Should().BeFalse();
    }
}
