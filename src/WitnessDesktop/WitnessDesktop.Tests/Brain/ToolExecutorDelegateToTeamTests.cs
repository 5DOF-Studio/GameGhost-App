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

    private ToolExecutor CreateSut(IGaimerTeamService? team = null)
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
            gaimerTeam: team);
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
}
