// File: src/WitnessDesktop/WitnessDesktop.Tests/Services/GeminiVideoClientTests.cs
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using WitnessDesktop.Services.Replay;
using Xunit;

namespace WitnessDesktop.Tests.Services;

public class GeminiVideoClientTests : IDisposable
{
    private readonly QueuedHttpHandler _handler;
    private readonly GeminiVideoClient _sut;

    public GeminiVideoClientTests()
    {
        _handler = new QueuedHttpHandler();
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") };
        _sut = new GeminiVideoClient(httpClient, "test-api-key");
    }

    public void Dispose() => _handler.Dispose();

    // --- Upload ---

    [Fact]
    public async Task UploadVideoAsync_ReturnsFileMetadata()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            file = new { name = "files/abc123", uri = "https://gen.ai/v1beta/files/abc123", state = "PROCESSING" }
        });

        // Create a temp file to upload
        var tmpFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tmpFile, new byte[] { 0x00, 0x01, 0x02 });
            var result = await _sut.UploadVideoAsync(tmpFile, CancellationToken.None);
            result.Name.Should().Be("files/abc123");
            result.Uri.Should().Contain("abc123");
            result.State.Should().Be("PROCESSING");
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // --- WaitForActive ---

    [Fact]
    public async Task WaitForActiveAsync_PollsUntilActive()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { name = "files/abc123", state = "PROCESSING" });
        _handler.Enqueue(HttpStatusCode.OK, new { name = "files/abc123", state = "ACTIVE" });

        await _sut.WaitForActiveAsync("files/abc123", CancellationToken.None);

        _handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task WaitForActiveAsync_ThrowsOnTimeout()
    {
        // Always return PROCESSING — should timeout
        for (int i = 0; i < 100; i++)
            _handler.Enqueue(HttpStatusCode.OK, new { name = "files/abc123", state = "PROCESSING" });

        var handler2 = _handler; // same queued handler, already has 100 items enqueued
        var sut = new GeminiVideoClient(
            new HttpClient(handler2) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") },
            "test-key",
            pollingTimeoutSeconds: 3,
            pollingIntervalMs: 100);

        var act = () => sut.WaitForActiveAsync("files/abc123", CancellationToken.None);
        await act.Should().ThrowAsync<TimeoutException>();
    }

    // --- GenerateContent ---

    [Fact]
    public async Task GenerateContentAsync_ReturnsText()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            candidates = new[] {
                new { content = new { parts = new[] { new { text = "{\"beats\":[]}" } }, role = "model" } }
            }
        });

        var result = await _sut.GenerateContentAsync("https://gen.ai/files/abc", "Analyze this.", "gemini-2.5-flash", CancellationToken.None);
        result.Should().Contain("beats");
    }

    [Fact]
    public async Task GenerateContentAsync_Throws_On429()
    {
        _handler.Enqueue(HttpStatusCode.TooManyRequests, new { error = new { message = "Rate limited" } });

        var act = () => _sut.GenerateContentAsync("https://gen.ai/files/abc", "Analyze", "gemini-2.5-flash", CancellationToken.None);
        await act.Should().ThrowAsync<GeminiRateLimitException>();
    }

    // --- Delete ---

    [Fact]
    public async Task DeleteFileAsync_Succeeds()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { });

        await _sut.DeleteFileAsync("files/abc123", CancellationToken.None);
        _handler.RequestCount.Should().Be(1);
    }

    // --- Cleanup Sweep ---

    [Fact]
    public async Task CleanupStaleFilesAsync_DeletesOldFiles()
    {
        var staleTime = DateTimeOffset.UtcNow.AddMinutes(-15).ToString("O");
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            files = new[] {
                new { name = "files/stale1", createTime = staleTime, state = "ACTIVE" },
                new { name = "files/fresh1", createTime = DateTimeOffset.UtcNow.ToString("O"), state = "ACTIVE" }
            }
        });
        _handler.Enqueue(HttpStatusCode.OK, new { }); // delete stale1

        var deleted = await _sut.CleanupStaleFilesAsync(TimeSpan.FromMinutes(10), CancellationToken.None);
        deleted.Should().Be(1);
    }

    // --- Truncation Repair ---

    [Fact]
    public void RepairTruncatedJson_ClosesArray()
    {
        var truncated = """[{"start":"0:00","end":"0:03","assessment":"Player moved"},{"start":"0:03","end":""";
        var repaired = GeminiVideoClient.RepairTruncatedJson(truncated);
        var act = () => JsonDocument.Parse(repaired);
        act.Should().NotThrow();
    }

    [Fact]
    public void RepairTruncatedJson_ValidJson_Untouched()
    {
        var valid = """[{"start":"0:00","end":"0:03","assessment":"ok"}]""";
        GeminiVideoClient.RepairTruncatedJson(valid).Should().Be(valid);
    }
}

/// <summary>
/// Simple queued HTTP handler that serves responses in order.
/// Named QueuedHttpHandler to avoid conflict with WitnessDesktop.Tests.Helpers.MockHttpHandler.
/// </summary>
internal class QueuedHttpHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    public int RequestCount { get; private set; }

    public void Enqueue(HttpStatusCode status, object body)
    {
        var json = JsonSerializer.Serialize(body);
        _responses.Enqueue(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        RequestCount++;
        if (_responses.Count == 0)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        return Task.FromResult(_responses.Dequeue());
    }
}
