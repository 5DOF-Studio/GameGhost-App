using System.Threading.Channels;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Integration;

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
        var timeline = new TimelineFeed();

        var router = new BrainEventRouter(timeline);
        router.StartConsuming(channel.Reader, CancellationToken.None);

        // Write 3 results: 2 ImageAnalysis (TopStrip only per D-039) + 1 ToolResult (timeline)
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

        // Allow consumer task + main-thread-dispatched timeline mutations to drain.
        // D-039: only ToolResult goes to timeline (1 event); ImageAnalysis goes to TopStrip only.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!cts.IsCancellationRequested)
        {
            if (timeline.Events.Count >= 1)
            {
                break;
            }
            await Task.Delay(50, cts.Token);
        }
        router.StopConsuming();

        // Verify ToolResult event was routed to timeline
        timeline.Events.Should().NotBeEmpty();
        timeline.Events.Should().HaveCountGreaterThanOrEqualTo(1);
        timeline.Events.Should().NotContain(e => e.Type == EventOutputType.ImageAnalysis);
    }
}
