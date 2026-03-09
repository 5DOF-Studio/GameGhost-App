using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;
using GaimerDesktop.Models;
using GaimerDesktop.Models.Timeline;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Brain;
using GaimerDesktop.Services.Conversation;
using GaimerDesktop.Services.Conversation.Providers;
using GaimerDesktop.Tests.Helpers;
using Xunit;

namespace GaimerDesktop.Tests.Integration;

/// <summary>
/// Integration seam tests that wire REAL components together (not mocks)
/// to verify critical pipeline paths work end-to-end.
/// </summary>
public class PipelineIntegrationTests
{
    #region FrameDiff -> Brain -> Channel -> Router

    [Fact]
    public async Task FrameDiff_ToBrain_ToChannel_ToRouter_EndToEnd()
    {
        // Arrange: Real FrameDiffService, MockBrainService, real TimelineFeed + BrainEventRouter
        var frameDiff = new FrameDiffService();
        var logger = NullLogger<MockBrainService>.Instance;
        var brain = new MockBrainService(logger);

        var sessionManager = new SessionManager();
        var timeline = new TimelineFeed(sessionManager);
        var contextService = new BrainContextService(new VisualReelService());
        var router = new BrainEventRouter(timeline, brainContext: contextService);

        // Create two distinct images so FrameDiffService detects change
        var image1 = TestImageFactory.CreateGradientPng(100, 100, SKColors.Black, SKColors.White);
        var image2 = TestImageFactory.CreateCheckerboardPng(100, 100, 10, SKColors.Red, SKColors.Blue);

        // Seed the diff service with first image
        frameDiff.HasChanged(image1, threshold: 5);

        // Act: check second image (should detect change)
        var changed = frameDiff.HasChanged(image2, threshold: 5);
        changed.Should().BeTrue("two visually different images should produce different hashes");

        // Submit to brain (MockBrainService writes to channel after 500ms delay)
        await brain.SubmitImageAsync(image2, "chess position");

        // Start router consuming from brain channel
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        router.StartConsuming(brain.Results, cts.Token);

        // Poll for the router to consume the brain result (MockBrainService has 500ms delay)
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (timeline.Checkpoints.Any()) break;
            await Task.Delay(50);
        }

        // Assert: timeline should have events from the routed brain result
        timeline.Checkpoints.Should().NotBeEmpty("router should have created timeline events from brain result");
        var checkpoint = timeline.Checkpoints.First();
        checkpoint.EventLines.Should().NotBeEmpty();

        // Verify a brain event was also ingested into context (L1)
        var envelope = await contextService.GetContextForVoiceAsync(DateTime.UtcNow);
        envelope.ImmediateEvents.Should().NotBeEmpty("brain result should be ingested as L1 event");

        // Cleanup
        router.StopConsuming();
        brain.Dispose();
    }

    #endregion

    #region SessionManager Tool Consistency

    [Fact]
    public void SessionTransition_ToolAvailability_Consistency()
    {
        // Arrange: real SessionManager
        var session = new SessionManager();

        // Act & Assert: OutGame tools
        session.CurrentState.Should().Be(SessionState.OutGame);
        var outTools = session.GetAvailableTools();
        outTools.Select(t => t.Name).Should().Contain("web_search");
        outTools.Select(t => t.Name).Should().NotContain("capture_screen");
        outTools.Select(t => t.Name).Should().NotContain("analyze_position_engine");

        // Transition to InGame
        session.TransitionToInGame("game-1", "chess", "lichess");
        session.CurrentState.Should().Be(SessionState.InGame);
        var inTools = session.GetAvailableTools();
        inTools.Select(t => t.Name).Should().Contain("capture_screen");
        inTools.Select(t => t.Name).Should().Contain("get_game_state");
        inTools.Select(t => t.Name).Should().Contain("analyze_position_engine");
        inTools.Select(t => t.Name).Should().Contain("analyze_position_strategic");

        // All OutGame tools still available InGame
        foreach (var outTool in outTools)
        {
            inTools.Select(t => t.Name).Should().Contain(outTool.Name,
                $"OutGame tool '{outTool.Name}' should also be available InGame");
        }

        // Back to OutGame
        session.TransitionToOutGame();
        var resetTools = session.GetAvailableTools();
        resetTools.Select(t => t.Name).Should().BeEquivalentTo(outTools.Select(t => t.Name));
    }

    #endregion

    #region BrainContext L1 Ingest -> Voice Context Flow

    [Fact]
    public async Task BrainContext_L1Ingest_ToVoiceContext_Flow()
    {
        // Arrange: real BrainContextService + real VisualReelService
        var reelService = new VisualReelService();
        var contextService = new BrainContextService(reelService);

        var now = DateTime.UtcNow;

        // Ingest L1 events
        await contextService.IngestEventAsync(new BrainEvent
        {
            TimestampUtc = now,
            Type = BrainEventType.VisionObservation,
            Category = "vision",
            Text = "White knight on f3 threatens black pawn on e5",
            Confidence = 0.9
        });

        await contextService.IngestEventAsync(new BrainEvent
        {
            TimestampUtc = now.AddSeconds(-5),
            Type = BrainEventType.GameplayState,
            Category = "threat",
            Text = "Possible queen sacrifice on h7",
            Confidence = 0.8
        });

        // Act: get voice context
        var envelope = await contextService.GetContextForVoiceAsync(now);

        // Assert
        envelope.Should().NotBeNull();
        envelope.ImmediateEvents.Should().HaveCountGreaterOrEqualTo(2);
        envelope.ImmediateEvents.Should().Contain(e => e.Category == "vision");
        envelope.ImmediateEvents.Should().Contain(e => e.Category == "threat");

        // Threat should be high-priority (ranked first or early)
        var formatted = contextService.FormatAsPrefixedContextBlock(envelope);
        formatted.Should().Contain("vision");
        formatted.Should().Contain("threat");
        formatted.Should().Contain("[Context]");
        formatted.Should().Contain("[End Context]");

        // Budget should be respected
        envelope.BudgetTokens.Should().Be(BrainContextService.DefaultVoiceBudget);
    }

    #endregion

    #region MockConversationProvider Lifecycle

    [Fact]
    public async Task ProviderFactory_ToProvider_ConnectState_Cycle()
    {
        // Arrange: factory with mock services config
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["USE_MOCK_SERVICES"] = "true"
            })
            .Build();

        var factory = new ConversationProviderFactory(config);

        // Act: create provider
        using var provider = factory.Create();

        // Assert: it's a MockConversationProvider
        provider.Should().BeOfType<MockConversationProvider>();
        provider.ProviderName.Should().Be("Mock Provider");
        provider.State.Should().Be(ConnectionState.Disconnected);
        provider.IsConnected.Should().BeFalse();

        // Connect
        var stateChanges = new List<ConnectionState>();
        provider.ConnectionStateChanged += (_, state) => stateChanges.Add(state);

        await provider.ConnectAsync(Agents.Chess);

        provider.State.Should().Be(ConnectionState.Connected);
        provider.IsConnected.Should().BeTrue();
        stateChanges.Should().Contain(ConnectionState.Connecting);
        stateChanges.Should().Contain(ConnectionState.Connected);

        // Disconnect
        await provider.DisconnectAsync();

        provider.State.Should().Be(ConnectionState.Disconnected);
        provider.IsConnected.Should().BeFalse();
    }

    #endregion

    #region Stockfish Agent Type Consistency

    [Fact]
    public void StockfishLifecycle_AgentType_Consistency()
    {
        // Verify that all chess agents have consistent properties for Stockfish integration

        // All chess agents should have Type == Chess
        var chessAgents = Agents.All.Where(a => a.Type == AgentType.Chess).ToList();
        chessAgents.Should().HaveCountGreaterOrEqualTo(2, "Leroy and Wasp should both be chess agents");

        foreach (var agent in chessAgents)
        {
            // Chess agents should be available
            agent.IsAvailable.Should().BeTrue($"chess agent '{agent.Name}' should be available");

            // Chess agents should have the chess tool set
            agent.Tools.Should().NotBeNull($"chess agent '{agent.Name}' should have tools");
            agent.Tools.Should().Contain("analyze_position_engine",
                $"chess agent '{agent.Name}' should have engine analysis tool");
            agent.Tools.Should().Contain("analyze_position_strategic",
                $"chess agent '{agent.Name}' should have strategic analysis tool");
            agent.Tools.Should().Contain("capture_screen",
                $"chess agent '{agent.Name}' should have screen capture tool");

            // System instruction should mention chess
            agent.SystemInstruction.Should().Contain("chess",
                $"chess agent '{agent.Name}' should have chess in system instruction");
        }

        // Verify Leroy and Wasp have different voice genders
        var leroy = Agents.GetByKey("chess");
        var wasp = Agents.GetByKey("wasp");
        leroy.Should().NotBeNull();
        wasp.Should().NotBeNull();
        leroy!.VoiceGender.Should().Be("male");
        wasp!.VoiceGender.Should().Be("female");

        // Both should reference the same tool set (same tools, same capture/brain info)
        leroy.Tools.Should().BeEquivalentTo(wasp.Tools);
        leroy.CaptureInfo.Should().Be(wasp.CaptureInfo);
        leroy.BrainInfo.Should().Be(wasp.BrainInfo);
    }

    #endregion

    #region Channel Pipeline Round-Trip

    [Fact]
    public async Task MockBrain_SubmitQuery_ResultsOnChannel()
    {
        // Verify the Channel<BrainResult> pipeline works with real MockBrainService
        var logger = NullLogger<MockBrainService>.Instance;
        var brain = new MockBrainService(logger);

        brain.IsBusy.Should().BeFalse();
        brain.ProviderName.Should().Be("Mock Brain");

        // Submit a query
        await brain.SubmitQueryAsync("What is the best move?", new SharedContextEnvelope
        {
            RequestTsUtc = DateTime.UtcNow,
            Intent = "move_advice",
            BudgetTokens = 900
        });

        // Read from channel with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var hasResult = await brain.Results.WaitToReadAsync(cts.Token);
        hasResult.Should().BeTrue("MockBrainService should produce a result");

        brain.Results.TryRead(out var result).Should().BeTrue();
        result.Should().NotBeNull();
        result!.Type.Should().Be(BrainResultType.ToolResult);
        result.AnalysisText.Should().NotBeNullOrEmpty();
        result.VoiceNarration.Should().NotBeNullOrEmpty();

        brain.Dispose();
    }

    #endregion
}
