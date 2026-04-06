using System.Reflection;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Services.Local;
using WitnessDesktop.Tests.Helpers;

namespace WitnessDesktop.Tests.Integration;

/// <summary>
/// Verifies that DI wiring passes all optional services (especially ITelemetryService)
/// to brain pipeline services. Regression test for the silent telemetry drop bug
/// where MauiProgram constructed ToolExecutor and OpenRouterBrainService without
/// passing the registered ITelemetryService, causing all pipeline telemetry to be null-dropped.
/// </summary>
public class DiWiringTests
{
    /// <summary>
    /// Simulates DI construction of ToolExecutor as MauiProgram does it.
    /// Verifies that telemetry is actually stored (non-null) when provided.
    /// Before the fix, MauiProgram omitted the telemetry: named parameter,
    /// so _telemetry was always null despite ITelemetryService being registered.
    /// </summary>
    [Fact]
    public void ToolExecutor_WhenTelemetryProvided_StoresItNonNull()
    {
        // Arrange — mirror MauiProgram DI construction
        var mockCapture = new Mock<IWindowCaptureService>();
        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.Context).Returns(new SessionContext());
        mockSession.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>());
        var httpClient = new HttpClient(MockHttpHandler.FromJson("{}"));
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");
        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var telemetry = new Mock<ITelemetryService>();

        // Act — construct with telemetry (as MauiProgram SHOULD do)
        var toolExecutor = new ToolExecutor(
            mockCapture.Object,
            mockSession.Object,
            client,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            mockLogger.Object,
            telemetry: telemetry.Object,
            gameJournal: null);

        // Assert — verify _telemetry field is non-null via reflection
        var field = typeof(ToolExecutor).GetField("_telemetry", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var value = field!.GetValue(toolExecutor);
        Assert.NotNull(value);
    }

    /// <summary>
    /// Proves the bug: when telemetry is omitted (as old MauiProgram did),
    /// _telemetry is null and TrackEvent calls are silently dropped.
    /// </summary>
    [Fact]
    public async Task ToolExecutor_WithoutTelemetry_SilentlyDropsTrackEvents()
    {
        // Arrange — construct WITHOUT telemetry (the old broken DI pattern)
        var mockCapture = new Mock<IWindowCaptureService>();
        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "test-game",
            GameType = "chess",
            ConnectorName = "test"
        });
        mockSession.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>
        {
            new ToolDefinition { Name = "game_journal", Description = "Get game journal" }
        });
        var httpClient = new HttpClient(MockHttpHandler.FromJson("{}"));
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");
        var mockLogger = new Mock<ILogger<ToolExecutor>>();

        var toolExecutor = new ToolExecutor(
            mockCapture.Object,
            mockSession.Object,
            client,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            mockLogger.Object);
        // NOTE: no telemetry param — this is the bug

        // Act
        var result = await toolExecutor.ExecuteToolAsync("game_journal", "{}", CancellationToken.None);

        // Assert — _telemetry is null, so no tracking happens (silent failure)
        var field = typeof(ToolExecutor).GetField("_telemetry", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Null(field!.GetValue(toolExecutor));
    }

    /// <summary>
    /// Simulates DI construction of OpenRouterBrainService as MauiProgram does it.
    /// Verifies that telemetry is stored when provided.
    /// </summary>
    [Fact]
    public void OpenRouterBrainService_WhenTelemetryProvided_StoresItNonNull()
    {
        // Arrange
        var httpClient = new HttpClient(MockHttpHandler.FromJson("{}"));
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");

        var mockCapture = new Mock<IWindowCaptureService>();
        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.Context).Returns(new SessionContext());
        mockSession.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>());
        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var telemetry = new Mock<ITelemetryService>();

        var toolExecutor = new ToolExecutor(
            mockCapture.Object, mockSession.Object, client,
            new MockStockfishService(), "openai/gpt-4o-mini", mockLogger.Object,
            telemetry: telemetry.Object);

        // Act — construct with telemetry (as MauiProgram SHOULD do)
        var brainService = new OpenRouterBrainService(
            client, toolExecutor, mockSession.Object,
            brainPromptBuilder: new BrainPromptBuilder(),
            telemetry: telemetry.Object,
            gameJournal: null,
            brainContext: null);

        // Assert — verify _telemetry field is non-null
        var field = typeof(OpenRouterBrainService).GetField("_telemetry", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var value = field!.GetValue(brainService);
        Assert.NotNull(value);

        // Cleanup
        brainService.Dispose();
    }

    /// <summary>
    /// Proves the bug: when telemetry is omitted from OpenRouterBrainService,
    /// all brain pipeline telemetry (frame_queued, submit_image, response_received, etc.) is lost.
    /// </summary>
    [Fact]
    public void OpenRouterBrainService_WithoutTelemetry_HasNullTelemetryField()
    {
        // Arrange — construct WITHOUT telemetry (the old broken DI pattern)
        var httpClient = new HttpClient(MockHttpHandler.FromJson("{}"));
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");

        var mockCapture = new Mock<IWindowCaptureService>();
        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.Context).Returns(new SessionContext());
        mockSession.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>());
        var mockLogger = new Mock<ILogger<ToolExecutor>>();

        var toolExecutor = new ToolExecutor(
            mockCapture.Object, mockSession.Object, client,
            new MockStockfishService(), "openai/gpt-4o-mini", mockLogger.Object);

        // Act — no telemetry param (old MauiProgram pattern)
        var brainService = new OpenRouterBrainService(
            client, toolExecutor, mockSession.Object,
            brainPromptBuilder: new BrainPromptBuilder());
        // NOTE: no telemetry — this is the bug

        // Assert — _telemetry is null
        var field = typeof(OpenRouterBrainService).GetField("_telemetry", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Null(field!.GetValue(brainService));

        // Cleanup
        brainService.Dispose();
    }

    /// <summary>
    /// End-to-end: when telemetry IS wired, executing a tool fires TrackEvent.
    /// This is the behavior we expect after fixing MauiProgram DI wiring.
    /// </summary>
    [Fact]
    public async Task ToolExecutor_WithTelemetry_FiresTrackEventOnToolExecution()
    {
        // Arrange
        var mockCapture = new Mock<IWindowCaptureService>();
        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "test-game",
            GameType = "chess",
            ConnectorName = "test"
        });
        mockSession.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>
        {
            new ToolDefinition { Name = "game_journal", Description = "Get game journal" }
        });
        var httpClient = new HttpClient(MockHttpHandler.FromJson("{}"));
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");
        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var mockTelemetry = new Mock<ITelemetryService>();

        var toolExecutor = new ToolExecutor(
            mockCapture.Object,
            mockSession.Object,
            client,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            mockLogger.Object,
            telemetry: mockTelemetry.Object,
            gameJournal: new GameJournalService());

        // Act
        await toolExecutor.ExecuteToolAsync("game_journal", "{}", CancellationToken.None);

        // Assert — telemetry was called for tool execution
        mockTelemetry.Verify(t => t.TrackEvent(
            "tool", "called",
            It.Is<Dictionary<string, string>>(d => d["toolName"] == "game_journal")),
            Times.Once);
        mockTelemetry.Verify(t => t.TrackEvent(
            "tool", "completed",
            It.Is<Dictionary<string, string>>(d => d["toolName"] == "game_journal")),
            Times.Once);
    }

    /// <summary>
    /// Verifies that BrainServiceFactory creates brain service through policy resolution,
    /// not direct OpenRouterBrainService construction. This is the key DI migration proof.
    /// </summary>
    [Fact]
    public async Task BrainServiceFactory_InCloudOnlyMode_ProducesNonNullBrainService()
    {
        var settings = new SettingsService();
        settings.InferenceMode = InferenceMode.CloudOnly;

        var policy = new InferenceProviderPolicy();
        var runtime = new MockLocalModelRuntime();
        var factory = new BrainServiceFactory(policy, settings, runtime);

        var result = await factory.CreateAsync(
            createCloudBrain: () =>
            {
                // Simulate cloud brain construction
                var httpClient = new HttpClient(MockHttpHandler.FromJson("{}"));
                var client = new OpenRouterClient(httpClient, "test-key", "test-model");
                var mockSession = new Mock<ISessionManager>();
                mockSession.Setup(s => s.Context).Returns(new SessionContext());
                mockSession.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>());
                var toolExec = new ToolExecutor(
                    new Mock<IWindowCaptureService>().Object,
                    mockSession.Object, client,
                    new MockStockfishService(), "openai/gpt-4o-mini",
                    new Mock<ILogger<ToolExecutor>>().Object);
                return new OpenRouterBrainService(client, toolExec, mockSession.Object);
            },
            createLocalBrain: () => throw new InvalidOperationException("Should not create local brain in CloudOnly mode"),
            createMockBrain: () => new MockBrainService(Mock.Of<ILogger<MockBrainService>>()),
            cloudBrainAvailable: true);

        result.Brain.Should().NotBeNull();
        result.Brain.Should().BeOfType<OpenRouterBrainService>();
        result.Selection.Mode.Should().Be(InferenceMode.CloudOnly);
        result.Brain.Dispose();
    }
}
