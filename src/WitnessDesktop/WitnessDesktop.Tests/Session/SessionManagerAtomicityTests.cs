using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Session;

public class SessionManagerAtomicityTests
{
    [Fact]
    public void ConcurrentTransitions_DoNotCorruptState()
    {
        // Arrange
        var sut = new SessionManager();
        var exceptions = new List<Exception>();
        const int iterations = 10;

        // Act — launch 10 parallel tasks, half InGame, half OutGame
        var tasks = new Task[iterations];
        for (int i = 0; i < iterations; i++)
        {
            int idx = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    if (idx % 2 == 0)
                        sut.TransitionToInGame($"game-{idx}", "chess", "lichess");
                    else
                        sut.TransitionToOutGame();
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert — no exceptions thrown, state is valid (either InGame or OutGame)
        exceptions.Should().BeEmpty("concurrent transitions should not throw");
        sut.CurrentState.Should().BeOneOf(
            new[] { SessionState.InGame, SessionState.OutGame },
            "state must be a valid enum value after concurrent transitions");

        // State consistency: if InGame, context fields should be set; if OutGame, should be null
        if (sut.CurrentState == SessionState.InGame)
        {
            sut.Context.GameId.Should().NotBeNull("GameId should be set when InGame");
            sut.Context.GameType.Should().NotBeNull("GameType should be set when InGame");
            sut.Context.ConnectorName.Should().NotBeNull("ConnectorName should be set when InGame");
            sut.Context.GameStartedAt.Should().NotBeNull("GameStartedAt should be set when InGame");
        }
        else
        {
            sut.Context.GameId.Should().BeNull("GameId should be null when OutGame");
            sut.Context.GameType.Should().BeNull("GameType should be null when OutGame");
            sut.Context.ConnectorName.Should().BeNull("ConnectorName should be null when OutGame");
            sut.Context.GameStartedAt.Should().BeNull("GameStartedAt should be null when OutGame");
        }
    }

    [Fact]
    public void TransitionToInGame_SetsAllFields_Atomically()
    {
        // Arrange
        var sut = new SessionManager();
        var stateSnapshot = new List<(SessionState State, string? GameId, string? GameType, string? ConnectorName, DateTime? GameStartedAt)>();

        sut.StateChanged += (_, state) =>
        {
            // Capture a snapshot of ALL context fields at the moment StateChanged fires
            lock (stateSnapshot)
            {
                stateSnapshot.Add((
                    sut.CurrentState,
                    sut.Context.GameId,
                    sut.Context.GameType,
                    sut.Context.ConnectorName,
                    sut.Context.GameStartedAt
                ));
            }
        };

        // Act
        sut.TransitionToInGame("game-atomic", "chess", "lichess");

        // Assert — when the event fired, ALL fields were already set (no partial write)
        stateSnapshot.Should().HaveCount(1, "StateChanged should fire exactly once");
        var snap = stateSnapshot[0];
        snap.State.Should().Be(SessionState.InGame, "state should be InGame when event fires");
        snap.GameId.Should().Be("game-atomic", "GameId should be set before event fires");
        snap.GameType.Should().Be("chess", "GameType should be set before event fires");
        snap.ConnectorName.Should().Be("lichess", "ConnectorName should be set before event fires");
        snap.GameStartedAt.Should().NotBeNull("GameStartedAt should be set before event fires");
    }
}
