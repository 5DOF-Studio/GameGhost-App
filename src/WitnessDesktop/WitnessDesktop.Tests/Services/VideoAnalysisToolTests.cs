using System.Text.Json;
using FluentAssertions;
using Moq;
using WitnessDesktop.Models;
using WitnessDesktop.Services.Replay;
using Xunit;

namespace WitnessDesktop.Tests.Services;

public class VideoAnalysisToolTests
{
    private readonly Mock<GeminiVideoClient> _mockClient;
    private readonly VideoAnalysisTool _sut;

    private static readonly GameSkillPack TestPack = new()
    {
        Id = "chess",
        Name = "Chess",
        Genre = "strategy",
        BrainInstructionsContent = "Analyze chess positions. Identify threats and best moves.",
        ObservationSchema = new ObservationSchema
        {
            SchemaName = "chess_observation",
            Fields = new List<ObservationField>
            {
                new() { Key = "position_assessment", Type = "string", Required = true, Description = "Board position assessment" },
                new() { Key = "suggested_action", Type = "string", Required = true, Description = "Best move recommendation" }
            }
        }
    };

    public VideoAnalysisToolTests()
    {
        _mockClient = new Mock<GeminiVideoClient>(
            new HttpClient(), "test-key", 120, 2000) { CallBase = false };
        _sut = new VideoAnalysisTool(_mockClient.Object, "gemini-2.5-flash", "gemini-3-pro-preview");
    }

    private static ReplaySegment MakeSegment(string path = "/tmp/test.mp4", int index = 0)
    {
        return new ReplaySegment
        {
            FilePath = path,
            SessionId = "sess-abc",
            StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
            EndUtc = DateTimeOffset.UtcNow,
            ByteSize = 90_000_000,
            SegmentIndex = index
        };
    }

    private static string MakeGeminiResponse(string summary = "Player castled kingside, opponent attacked", params string[] assessments)
    {
        if (assessments.Length == 0) assessments = new[] { "Pawn to e4", "Knight develops to f3" };
        var beats = assessments.Select((a, i) => new
        {
            start_time = $"0:{i * 3:D2}",
            end_time = $"0:{(i + 1) * 3:D2}",
            signal = "none",
            urgency = "low",
            assessment = a
        });
        return JsonSerializer.Serialize(new
        {
            beats = beats,
            narrative_summary = summary
        });
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsResult()
    {
        var geminiResponse = MakeGeminiResponse();
        _mockClient.Setup(c => c.UploadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiFileMetadata { Name = "files/x", Uri = "https://gen.ai/files/x", State = "PROCESSING" });
        _mockClient.Setup(c => c.WaitForActiveAsync("files/x", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockClient.Setup(c => c.GenerateContentAsync("https://gen.ai/files/x", It.IsAny<string>(), "gemini-2.5-flash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(geminiResponse);
        _mockClient.Setup(c => c.DeleteFileAsync("files/x", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.AnalyzeAsync(MakeSegment(), TestPack);
        result.Beats.Should().HaveCount(2);
        result.NarrativeSummary.Should().Contain("castled");
        result.Model.Should().Be("gemini-2.5-flash");
    }

    [Fact]
    public async Task AnalyzeAsync_DeletesFileOnException()
    {
        _mockClient.Setup(c => c.UploadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiFileMetadata { Name = "files/x", Uri = "https://gen.ai/files/x", State = "PROCESSING" });
        _mockClient.Setup(c => c.WaitForActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());

        var act = () => _sut.AnalyzeAsync(MakeSegment(), TestPack);
        await act.Should().ThrowAsync<TimeoutException>();

        _mockClient.Verify(c => c.DeleteFileAsync("files/x", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_UsesProModel()
    {
        _mockClient.Setup(c => c.UploadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiFileMetadata { Name = "files/x", Uri = "https://gen.ai/files/x", State = "PROCESSING" });
        _mockClient.Setup(c => c.WaitForActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var searchResponse = JsonSerializer.Serialize(new
        {
            hits = new[] { new { start_time = "0:30", end_time = "0:45", description = "Player died to flank", confidence = "HIGH" } },
            summary = "One death found at 0:30"
        });
        _mockClient.Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), "gemini-3-pro-preview", It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);
        _mockClient.Setup(c => c.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.SearchAsync(new[] { MakeSegment() }, TestPack, "how did I die");
        result.Hits.Should().HaveCount(1);
        result.Summary.Should().Contain("death");
    }

    [Fact]
    public async Task CircuitBreaker_TripsAfter3RateLimits()
    {
        _mockClient.Setup(c => c.UploadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiFileMetadata { Name = "files/x", Uri = "https://gen.ai/files/x", State = "PROCESSING" });
        _mockClient.Setup(c => c.WaitForActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockClient.Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GeminiRateLimitException("429"));
        _mockClient.Setup(c => c.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        for (int i = 0; i < 3; i++)
        {
            try { await _sut.AnalyzeAsync(MakeSegment(), TestPack); } catch { }
        }

        _sut.IsCircuitBroken.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_SkipsWhenCircuitBroken()
    {
        // Trip the breaker
        _mockClient.Setup(c => c.UploadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiFileMetadata { Name = "files/x", Uri = "https://gen.ai/files/x", State = "PROCESSING" });
        _mockClient.Setup(c => c.WaitForActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockClient.Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GeminiRateLimitException("429"));
        _mockClient.Setup(c => c.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        for (int i = 0; i < 3; i++)
        {
            try { await _sut.AnalyzeAsync(MakeSegment(), TestPack); } catch { }
        }

        // Next call should throw immediately without uploading
        _mockClient.Invocations.Clear();
        var act = () => _sut.AnalyzeAsync(MakeSegment(), TestPack);
        await act.Should().ThrowAsync<GeminiRateLimitException>().WithMessage("*circuit*");

        _mockClient.Verify(c => c.UploadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_EmptySegments_ReturnsGuardResult()
    {
        var result = await _sut.SearchAsync(Array.Empty<ReplaySegment>(), TestPack, "how did I die");
        result.Hits.Should().BeEmpty();
        result.Summary.Should().Be("No segments available for search");
        result.Query.Should().Be("how did I die");
    }

    [Fact]
    public async Task AnalyzeAsync_PromptContainsPackInstructions()
    {
        string? capturedPrompt = null;
        _mockClient.Setup(c => c.UploadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiFileMetadata { Name = "files/x", Uri = "https://gen.ai/files/x", State = "PROCESSING" });
        _mockClient.Setup(c => c.WaitForActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockClient.Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, prompt, _, _) => capturedPrompt = prompt)
            .ReturnsAsync(MakeGeminiResponse());
        _mockClient.Setup(c => c.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.AnalyzeAsync(MakeSegment(), TestPack);
        capturedPrompt.Should().Contain("Analyze chess positions");
        capturedPrompt.Should().Contain("3-second");
    }
}
