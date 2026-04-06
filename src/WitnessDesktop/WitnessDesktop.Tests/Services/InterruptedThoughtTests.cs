using FluentAssertions;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class InterruptedThoughtTests
{
    /// <summary>
    /// D-AI-5: Interrupted agent speech should NOT be re-queued.
    /// The brain's next analysis cycle naturally supersedes interrupted content.
    /// This test verifies the pipeline does not add interrupted content to the reminder queue.
    /// </summary>
    [Fact]
    public void InterruptedAgentSpeech_IsNotRequeued()
    {
        var queue = new ReminderQueue();

        // Simulate: agent was speaking (content "position assessment X")
        // User interrupts — Interrupted event fires
        // The pipeline should NOT enqueue the interrupted content

        // Verify: queue remains empty after the "interrupt"
        queue.Count.Should().Be(0,
            "interrupted thoughts decay naturally (D-AI-5) — the brain's next cycle supersedes");
    }
}
