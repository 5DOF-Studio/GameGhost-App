using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Context;

public class VoiceTranscriptStoreTests
{
    [Fact]
    public void VoiceTranscriptTurn_DefaultTimestamp_IsUtcNow()
    {
        var before = DateTime.UtcNow;
        var turn = new VoiceTranscriptTurn { Role = TranscriptRole.User, Text = "hello" };
        var after = DateTime.UtcNow;

        turn.TimestampUtc.Should().BeOnOrAfter(before);
        turn.TimestampUtc.Should().BeOnOrBefore(after);
        turn.Role.Should().Be(TranscriptRole.User);
        turn.Text.Should().Be("hello");
    }

    [Fact]
    public void AddTurn_StoresAndReturnsInGetRecent()
    {
        var store = new VoiceTranscriptStore();
        var turn = new VoiceTranscriptTurn { Role = TranscriptRole.User, Text = "What should I do?" };

        store.AddTurn(turn);

        var recent = store.GetRecent(10);
        recent.Should().ContainSingle(t => t.Text == "What should I do?");
    }

    [Fact]
    public void GetRecent_ReturnsNewestFirst()
    {
        var store = new VoiceTranscriptStore();
        var now = DateTime.UtcNow;
        store.AddTurn(new VoiceTranscriptTurn { TimestampUtc = now.AddSeconds(-2), Role = TranscriptRole.User, Text = "first" });
        store.AddTurn(new VoiceTranscriptTurn { TimestampUtc = now.AddSeconds(-1), Role = TranscriptRole.Assistant, Text = "second" });
        store.AddTurn(new VoiceTranscriptTurn { TimestampUtc = now, Role = TranscriptRole.User, Text = "third" });

        var recent = store.GetRecent(10);
        recent.Should().HaveCount(3);
        recent[0].Text.Should().Be("third");
        recent[2].Text.Should().Be("first");
    }

    [Fact]
    public void GetRecent_RespectsMaxCount()
    {
        var store = new VoiceTranscriptStore();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
            store.AddTurn(new VoiceTranscriptTurn { TimestampUtc = now.AddSeconds(i), Role = TranscriptRole.User, Text = $"msg-{i}" });

        var recent = store.GetRecent(5);
        recent.Should().HaveCount(5);
        recent[0].Text.Should().Be("msg-19");
    }

    [Fact]
    public void AddTurn_PrunesTurnsOlderThan5Min()
    {
        var store = new VoiceTranscriptStore();
        var now = DateTime.UtcNow;
        store.AddTurn(new VoiceTranscriptTurn { TimestampUtc = now.AddMinutes(-6), Role = TranscriptRole.User, Text = "stale" });
        store.AddTurn(new VoiceTranscriptTurn { TimestampUtc = now, Role = TranscriptRole.User, Text = "fresh" });

        var recent = store.GetRecent(10);
        recent.Should().NotContain(t => t.Text == "stale");
        recent.Should().ContainSingle(t => t.Text == "fresh");
    }

    [Fact]
    public void AddTurn_CapsAt100Turns()
    {
        var store = new VoiceTranscriptStore();
        var baseTime = DateTime.UtcNow;
        for (int i = 0; i < 120; i++)
            store.AddTurn(new VoiceTranscriptTurn
            {
                TimestampUtc = baseTime.AddSeconds(i),
                Role = TranscriptRole.User,
                Text = $"msg-{i}"
            });

        var recent = store.GetRecent(200);
        recent.Should().HaveCount(100);
        recent[0].Text.Should().Be("msg-119"); // newest
    }

    [Fact]
    public void Flush_ClearsAllTurns()
    {
        var store = new VoiceTranscriptStore();
        store.AddTurn(new VoiceTranscriptTurn { Role = TranscriptRole.User, Text = "hello" });
        store.Flush();

        store.GetRecent(10).Should().BeEmpty();
    }

    [Fact]
    public void GetRecent_IsThreadSafe()
    {
        var store = new VoiceTranscriptStore();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var writer = Task.Run(() =>
        {
            int i = 0;
            while (!cts.Token.IsCancellationRequested)
                store.AddTurn(new VoiceTranscriptTurn { Role = TranscriptRole.User, Text = $"msg-{i++}" });
        });

        var reader = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var recent = store.GetRecent(10);
                recent.Should().NotBeNull();
            }
        });

        Task.WhenAll(writer, reader).Wait(TimeSpan.FromSeconds(3));
        // No exceptions = thread-safe
    }
}
