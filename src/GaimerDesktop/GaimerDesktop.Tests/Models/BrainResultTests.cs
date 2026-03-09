using GaimerDesktop.Models;

namespace GaimerDesktop.Tests.Models;

public class BrainResultTests
{
    [Fact]
    public void BrainResult_DefaultPriority_WhenIdle()
    {
        var result = new BrainResult { Type = BrainResultType.ImageAnalysis };

        result.Priority.Should().Be(BrainResultPriority.WhenIdle);
    }

    [Fact]
    public void BrainResult_DefaultCreatedAt_UtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var result = new BrainResult { Type = BrainResultType.ImageAnalysis };

        result.CreatedAt.Should().BeCloseTo(before, TimeSpan.FromSeconds(2));
    }
}
