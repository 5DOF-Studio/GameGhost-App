using System.Threading.Channels;
using GaimerDesktop.Models;
using GaimerDesktop.Models.Timeline;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Conversation;

namespace GaimerDesktop.Tests.Integration;

public class ChannelPipelineTests
{
    [Fact]
    public async Task ChannelBrainResult_MultipleProducers_ConsumerReadsAll()
    {
        var channel = Channel.CreateUnbounded<BrainResult>();
        var consumed = new List<BrainResult>();

        // Consumer
        var consumerTask = Task.Run(async () =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync())
            {
                consumed.Add(item);
            }
        });

        // Multiple producers write concurrently
        var producers = Enumerable.Range(0, 5).Select(i => Task.Run(async () =>
        {
            await channel.Writer.WriteAsync(new BrainResult
            {
                Type = BrainResultType.ImageAnalysis,
                AnalysisText = $"Analysis #{i}"
            });
        }));

        await Task.WhenAll(producers);
        channel.Writer.Complete();
        await consumerTask;

        consumed.Should().HaveCount(5);
        consumed.Select(r => r.AnalysisText).Should().OnlyContain(t => t!.StartsWith("Analysis #"));
    }

    [Fact]
    public async Task BrainEventRouter_ChannelConsumer_RoutesAllResults()
    {
        var channel = Channel.CreateUnbounded<BrainResult>();
        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.CurrentState).Returns(SessionState.InGame);
        var timeline = new TimelineFeed(mockSession.Object);

        var router = new BrainEventRouter(timeline);
        router.StartConsuming(channel.Reader, CancellationToken.None);

        // Write 3 results
        await channel.Writer.WriteAsync(new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Position looks sharp"
        });
        await channel.Writer.WriteAsync(new BrainResult
        {
            Type = BrainResultType.ToolResult,
            AnalysisText = "Eval: +150cp"
        });
        await channel.Writer.WriteAsync(new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Knight fork threat"
        });
        channel.Writer.Complete();

        // Allow consumer task to drain
        await Task.Delay(500);
        router.StopConsuming();

        // Verify events were routed to timeline
        timeline.Checkpoints.Should().NotBeEmpty();
        var allEvents = timeline.Checkpoints
            .SelectMany(cp => cp.EventLines)
            .SelectMany(el => el.Events)
            .ToList();
        allEvents.Should().HaveCountGreaterThanOrEqualTo(3);
    }
}
