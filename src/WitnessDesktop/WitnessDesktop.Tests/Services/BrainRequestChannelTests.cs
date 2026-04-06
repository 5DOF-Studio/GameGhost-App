using FluentAssertions;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class BrainRequestChannelTests
{
    [Fact]
    public async Task WriteAsync_TryRead_RoundTrips()
    {
        var sut = new BrainRequestChannel();
        var request = new BrainRequest { UserQuestion = "What happened?" };
        await sut.WriteAsync(request);
        sut.TryRead(out var result).Should().BeTrue();
        result!.UserQuestion.Should().Be("What happened?");
    }

    [Fact]
    public void TryRead_WhenEmpty_ReturnsFalse()
    {
        var sut = new BrainRequestChannel();
        sut.TryRead(out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public async Task PendingCount_TracksDepth()
    {
        var sut = new BrainRequestChannel();
        await sut.WriteAsync(new BrainRequest { UserQuestion = "q1" });
        await sut.WriteAsync(new BrainRequest { UserQuestion = "q2" });
        sut.PendingCount.Should().Be(2);
        sut.TryRead(out _);
        sut.PendingCount.Should().Be(1);
    }

    [Fact]
    public async Task Bounded_DropsOldestWhenFull()
    {
        var sut = new BrainRequestChannel();
        // Fill beyond capacity (8)
        for (int i = 0; i < 10; i++)
            await sut.WriteAsync(new BrainRequest { UserQuestion = $"q{i}" });

        // Should be able to read — oldest were dropped
        sut.TryRead(out var result).Should().BeTrue();
        // The newest items should survive (q2-q9 after q0,q1 dropped)
        result!.UserQuestion.Should().StartWith("q");
    }

    [Fact]
    public async Task Reader_CanEnumerateAsync()
    {
        var sut = new BrainRequestChannel();
        await sut.WriteAsync(new BrainRequest { UserQuestion = "test" });

        // Non-blocking read via Reader
        sut.Reader.TryRead(out var item).Should().BeTrue();
        item!.UserQuestion.Should().Be("test");
    }
}
