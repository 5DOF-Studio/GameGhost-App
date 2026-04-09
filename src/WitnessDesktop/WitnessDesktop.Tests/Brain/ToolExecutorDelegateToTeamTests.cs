using System.Collections.Immutable;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Tests.Helpers;

namespace WitnessDesktop.Tests.Brain;

public class ToolExecutorDelegateToTeamTests
{
    private readonly Mock<IWindowCaptureService> _mockCapture = new();
    private readonly Mock<ISessionManager> _mockSession = new();
    private readonly Mock<ILogger<ToolExecutor>> _mockLogger = new();
    private readonly Mock<IGaimerTeamService> _mockTeam = new();

    public ToolExecutorDelegateToTeamTests()
    {
        _mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "game-1",
            GameType = "chess",
            ConnectorName = "lichess",
            AgentKey = "chess"
        });
    }

    private ToolExecutor CreateSut(
        IGaimerTeamService? team = null,
        IBrainContextService? brainContext = null)
    {
        var httpClient = new HttpClient(MockHttpHandler.FromJson("{}"));
        var orClient = new OpenRouterClient(httpClient, "test-key", "test-model");
        return new ToolExecutor(
            _mockCapture.Object,
            _mockSession.Object,
            orClient,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            _mockLogger.Object,
            gaimerTeam: team,
            brainContext: brainContext);
    }

    [Fact]
    public async Task DelegateToTeam_WhenConnected_ReturnsSubmitted()
    {
        _mockTeam.Setup(t => t.IsConnected).Returns(true);
        _mockTeam.Setup(t => t.SubmitTaskAsync(It.IsAny<GaimerTeamTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("gt_test123456");
        var sut = CreateSut(_mockTeam.Object);

        var result = await sut.ExecuteToolAsync("delegate_to_team",
            """{"task":"Look up meta builds for Shoothouse"}""");

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("submitted");
        json.RootElement.GetProperty("task_id").GetString().Should().Be("gt_test123456");
    }

    [Fact]
    public async Task DelegateToTeam_WhenDisconnected_ReturnsUnavailable()
    {
        _mockTeam.Setup(t => t.IsConnected).Returns(false);
        var sut = CreateSut(_mockTeam.Object);

        var result = await sut.ExecuteToolAsync("delegate_to_team",
            """{"task":"Do something"}""");

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("unavailable");
    }

    [Fact]
    public async Task DelegateToTeam_WhenNoTeamService_ReturnsUnavailable()
    {
        var sut = CreateSut(team: null);

        var result = await sut.ExecuteToolAsync("delegate_to_team",
            """{"task":"Do something"}""");

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("unavailable");
    }

    [Fact]
    public async Task DelegateToTeam_AssemblesContext()
    {
        _mockTeam.Setup(t => t.IsConnected).Returns(true);
        GaimerTeamTask? captured = null;
        _mockTeam.Setup(t => t.SubmitTaskAsync(It.IsAny<GaimerTeamTask>(), It.IsAny<CancellationToken>()))
            .Callback<GaimerTeamTask, CancellationToken>((task, _) => captured = task)
            .ReturnsAsync("gt_abc");
        var sut = CreateSut(_mockTeam.Object);

        await sut.ExecuteToolAsync("delegate_to_team",
            """{"task":"Research opening theory","response_format":"detailed"}""");

        captured.Should().NotBeNull();
        captured!.Task.Should().Be("Research opening theory");
        captured.ResponseFormat.Should().Be("detailed");
        captured.Context.Game.Should().Be("chess");
        captured.Context.Agent.Should().Be("Leroy");
    }

    [Fact]
    public async Task DelegateToTeam_DefaultsResponseFormatToVoice()
    {
        _mockTeam.Setup(t => t.IsConnected).Returns(true);
        GaimerTeamTask? captured = null;
        _mockTeam.Setup(t => t.SubmitTaskAsync(It.IsAny<GaimerTeamTask>(), It.IsAny<CancellationToken>()))
            .Callback<GaimerTeamTask, CancellationToken>((task, _) => captured = task)
            .ReturnsAsync("gt_abc");
        var sut = CreateSut(_mockTeam.Object);

        await sut.ExecuteToolAsync("delegate_to_team",
            """{"task":"Something"}""");

        captured!.ResponseFormat.Should().Be("voice");
    }

    // ── Brain Context Population Tests ─────────────────────────────────────

    [Fact]
    public async Task DelegateToTeam_PopulatesL1Context_FromImmediateEvents()
    {
        _mockTeam.Setup(t => t.IsConnected).Returns(true);
        GaimerTeamTask? captured = null;
        _mockTeam.Setup(t => t.SubmitTaskAsync(It.IsAny<GaimerTeamTask>(), It.IsAny<CancellationToken>()))
            .Callback<GaimerTeamTask, CancellationToken>((task, _) => captured = task)
            .ReturnsAsync("gt_ctx1");

        var ts = new DateTime(2026, 4, 7, 14, 30, 0, DateTimeKind.Utc);
        var events = new List<BrainEvent>
        {
            new() { TimestampUtc = ts, Category = "threat", Text = "Enemy flanking left" },
            new() { TimestampUtc = ts.AddSeconds(5), Category = "objective", Text = "Plant bomb at A site" }
        };

        var mockBrainContext = new Mock<IBrainContextService>();
        mockBrainContext.Setup(bc => bc.GetContextForChatAsync(
                It.IsAny<DateTime>(),
                It.Is<string>(s => s == "delegation"),
                It.Is<int>(b => b == 2500),
                It.IsAny<ContextAssemblyInputs?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedContextEnvelope
            {
                ImmediateEvents = events,
                RollingSummary = "",
                RecentChatSummary = "",
                RecentVoiceTranscript = ""
            });

        var sut = CreateSut(_mockTeam.Object, mockBrainContext.Object);

        await sut.ExecuteToolAsync("delegate_to_team",
            """{"task":"Analyze the flank"}""");

        captured.Should().NotBeNull();
        captured!.Context.L1Context.Should().NotBeNull();
        captured.Context.L1Context.Should().Contain("[14:30:00] threat: Enemy flanking left");
        captured.Context.L1Context.Should().Contain("[14:30:05] objective: Plant bomb at A site");
    }

    [Fact]
    public async Task DelegateToTeam_PopulatesL2Context_FromRollingSummary()
    {
        _mockTeam.Setup(t => t.IsConnected).Returns(true);
        GaimerTeamTask? captured = null;
        _mockTeam.Setup(t => t.SubmitTaskAsync(It.IsAny<GaimerTeamTask>(), It.IsAny<CancellationToken>()))
            .Callback<GaimerTeamTask, CancellationToken>((task, _) => captured = task)
            .ReturnsAsync("gt_ctx2");

        var mockBrainContext = new Mock<IBrainContextService>();
        mockBrainContext.Setup(bc => bc.GetContextForChatAsync(
                It.IsAny<DateTime>(),
                It.Is<string>(s => s == "delegation"),
                It.Is<int>(b => b == 2500),
                It.IsAny<ContextAssemblyInputs?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedContextEnvelope
            {
                ImmediateEvents = ImmutableArray<BrainEvent>.Empty,
                RollingSummary = "Player has been aggressive, pushing mid control for 3 minutes",
                RecentChatSummary = "",
                RecentVoiceTranscript = ""
            });

        var sut = CreateSut(_mockTeam.Object, mockBrainContext.Object);

        await sut.ExecuteToolAsync("delegate_to_team",
            """{"task":"Suggest strategy change"}""");

        captured.Should().NotBeNull();
        captured!.Context.L2Context.Should().Be("Player has been aggressive, pushing mid control for 3 minutes");
    }

    [Fact]
    public async Task DelegateToTeam_PopulatesRecentActivity_FromChatAndVoice()
    {
        _mockTeam.Setup(t => t.IsConnected).Returns(true);
        GaimerTeamTask? captured = null;
        _mockTeam.Setup(t => t.SubmitTaskAsync(It.IsAny<GaimerTeamTask>(), It.IsAny<CancellationToken>()))
            .Callback<GaimerTeamTask, CancellationToken>((task, _) => captured = task)
            .ReturnsAsync("gt_ctx3");

        var mockBrainContext = new Mock<IBrainContextService>();
        mockBrainContext.Setup(bc => bc.GetContextForChatAsync(
                It.IsAny<DateTime>(),
                It.Is<string>(s => s == "delegation"),
                It.Is<int>(b => b == 2500),
                It.IsAny<ContextAssemblyInputs?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedContextEnvelope
            {
                ImmediateEvents = ImmutableArray<BrainEvent>.Empty,
                RollingSummary = "",
                RecentChatSummary = "User asked about opening theory",
                RecentVoiceTranscript = "User (voice): What should I play here?"
            });

        var sut = CreateSut(_mockTeam.Object, mockBrainContext.Object);

        await sut.ExecuteToolAsync("delegate_to_team",
            """{"task":"Research the opening"}""");

        captured.Should().NotBeNull();
        captured!.Context.RecentActivity.Should().NotBeNull();
        captured.Context.RecentActivity.Should().Contain("--- Recent Chat ---");
        captured.Context.RecentActivity.Should().Contain("User asked about opening theory");
        captured.Context.RecentActivity.Should().Contain("--- Recent Voice ---");
        captured.Context.RecentActivity.Should().Contain("What should I play here?");
    }

    [Fact]
    public async Task DelegateToTeam_EmptyEnvelope_LeavesContextFieldsNull()
    {
        _mockTeam.Setup(t => t.IsConnected).Returns(true);
        GaimerTeamTask? captured = null;
        _mockTeam.Setup(t => t.SubmitTaskAsync(It.IsAny<GaimerTeamTask>(), It.IsAny<CancellationToken>()))
            .Callback<GaimerTeamTask, CancellationToken>((task, _) => captured = task)
            .ReturnsAsync("gt_ctx4");

        var mockBrainContext = new Mock<IBrainContextService>();
        mockBrainContext.Setup(bc => bc.GetContextForChatAsync(
                It.IsAny<DateTime>(),
                It.Is<string>(s => s == "delegation"),
                It.Is<int>(b => b == 2500),
                It.IsAny<ContextAssemblyInputs?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedContextEnvelope
            {
                ImmediateEvents = ImmutableArray<BrainEvent>.Empty,
                RollingSummary = "",
                RecentChatSummary = "",
                RecentVoiceTranscript = ""
            });

        var sut = CreateSut(_mockTeam.Object, mockBrainContext.Object);

        await sut.ExecuteToolAsync("delegate_to_team",
            """{"task":"Do something"}""");

        captured.Should().NotBeNull();
        captured!.Context.L1Context.Should().BeNull();
        captured.Context.L2Context.Should().BeNull();
        captured.Context.RecentActivity.Should().BeNull();
    }

    [Fact]
    public async Task DelegateToTeam_NoBrainContextService_LeavesContextFieldsNull()
    {
        _mockTeam.Setup(t => t.IsConnected).Returns(true);
        GaimerTeamTask? captured = null;
        _mockTeam.Setup(t => t.SubmitTaskAsync(It.IsAny<GaimerTeamTask>(), It.IsAny<CancellationToken>()))
            .Callback<GaimerTeamTask, CancellationToken>((task, _) => captured = task)
            .ReturnsAsync("gt_ctx5");

        // No brainContext passed — null
        var sut = CreateSut(_mockTeam.Object, brainContext: null);

        await sut.ExecuteToolAsync("delegate_to_team",
            """{"task":"Do something"}""");

        captured.Should().NotBeNull();
        captured!.Context.L1Context.Should().BeNull();
        captured.Context.L2Context.Should().BeNull();
        captured.Context.RecentActivity.Should().BeNull();
    }
}
