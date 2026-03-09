using GaimerDesktop.Models;
using GaimerDesktop.Services;

namespace GaimerDesktop.Tests.Context;

public class VisualReelServiceTests
{
    private static ReelMoment MakeMoment(DateTime? timestamp = null, double confidence = 1.0)
    {
        return new ReelMoment
        {
            TimestampUtc = timestamp ?? DateTime.UtcNow,
            SourceTarget = "test-window",
            Confidence = confidence
        };
    }

    [Fact]
    public void Append_IncreasesCount()
    {
        var sut = new VisualReelService();
        sut.Append(MakeMoment());

        sut.GetRecent(10).Count.Should().Be(1);
    }

    [Fact]
    public void GetRecent_NewestFirst()
    {
        var sut = new VisualReelService();
        var t1 = DateTime.UtcNow.AddSeconds(-3);
        var t2 = DateTime.UtcNow.AddSeconds(-2);
        var t3 = DateTime.UtcNow.AddSeconds(-1);

        sut.Append(MakeMoment(t1));
        sut.Append(MakeMoment(t2));
        sut.Append(MakeMoment(t3));

        var recent = sut.GetRecent(10);
        recent.Should().HaveCount(3);
        recent[0].TimestampUtc.Should().Be(t3);
    }

    [Fact]
    public void GetRecent_RespectsCountLimit()
    {
        var sut = new VisualReelService();
        for (var i = 0; i < 20; i++)
            sut.Append(MakeMoment(DateTime.UtcNow.AddSeconds(-i)));

        sut.GetRecent(5).Count.Should().Be(5);
    }

    [Fact]
    public void GetRecent_Empty_ReturnsEmptyList()
    {
        var sut = new VisualReelService();
        var result = sut.GetRecent(10);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Trim_AgePolicy_RemovesOld()
    {
        var sut = new VisualReelService { MaxAgeSeconds = 60 };
        // Old moment (2 min ago)
        sut.Append(MakeMoment(DateTime.UtcNow.AddMinutes(-2)));
        // Recent moment
        sut.Append(MakeMoment(DateTime.UtcNow));

        // The trim happens on Append, so the old one should be removed
        sut.GetRecent(10).Count.Should().Be(1);
    }

    [Fact]
    public void Trim_CountPolicy_KeepsMaxCount()
    {
        var sut = new VisualReelService { MaxCount = 5, MaxAgeSeconds = 3600 };

        for (var i = 0; i < 10; i++)
            sut.Append(MakeMoment(DateTime.UtcNow.AddSeconds(-i)));

        sut.GetRecent(10).Count.Should().Be(5);
    }

    [Fact]
    public void Trim_AgeFirstThenCount()
    {
        var sut = new VisualReelService { MaxCount = 3, MaxAgeSeconds = 120 };

        // 2 old moments (3 min ago)
        sut.Append(MakeMoment(DateTime.UtcNow.AddMinutes(-3)));
        sut.Append(MakeMoment(DateTime.UtcNow.AddMinutes(-3).AddSeconds(1)));

        // 3 recent moments (within 120s)
        sut.Append(MakeMoment(DateTime.UtcNow.AddSeconds(-30)));
        sut.Append(MakeMoment(DateTime.UtcNow.AddSeconds(-20)));
        sut.Append(MakeMoment(DateTime.UtcNow.AddSeconds(-10)));

        // After trim: age removes 2 old, then count keeps max 3 recent
        var result = sut.GetRecent(10);
        result.Count.Should().Be(3);
    }
}
