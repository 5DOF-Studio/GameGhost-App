using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Context;

public sealed class ObservationAdmissionGateTests
{
    [Fact]
    public void Evaluate_FirstObservation_StoresAndAnalyzes()
    {
        var sut = new ObservationAdmissionGate();

        var decision = sut.Evaluate(hasMeaningfulChange: true, observedAtUtc: DateTime.UtcNow);

        decision.StoreObservation.Should().BeTrue();
        decision.SendToBrain.Should().BeTrue();
        decision.Reason.Should().Be("first_observation");
    }

    [Fact]
    public void Evaluate_ChangedWithinBrainCooldown_StoresOnly()
    {
        var now = DateTime.UtcNow;
        var sut = new ObservationAdmissionGate
        {
            BrainMinInterval = TimeSpan.FromSeconds(5)
        };

        sut.Evaluate(true, now);
        var decision = sut.Evaluate(true, now.AddSeconds(1));

        decision.StoreObservation.Should().BeTrue();
        decision.SendToBrain.Should().BeFalse();
        decision.Reason.Should().Be("changed_but_brain_cooldown");
    }

    [Fact]
    public void Evaluate_ChangedAfterBrainCooldown_StoresAndAnalyzes()
    {
        var now = DateTime.UtcNow;
        var sut = new ObservationAdmissionGate
        {
            BrainMinInterval = TimeSpan.FromSeconds(2)
        };

        sut.Evaluate(true, now);
        var decision = sut.Evaluate(true, now.AddSeconds(3));

        decision.StoreObservation.Should().BeTrue();
        decision.SendToBrain.Should().BeTrue();
        decision.Reason.Should().Be("changed_and_ready_for_brain");
    }

    [Fact]
    public void Evaluate_UnchangedBeforeKeepalive_Skips()
    {
        var now = DateTime.UtcNow;
        var sut = new ObservationAdmissionGate
        {
            KeepaliveInterval = TimeSpan.FromSeconds(10)
        };

        sut.Evaluate(true, now);
        var decision = sut.Evaluate(false, now.AddSeconds(2));

        decision.StoreObservation.Should().BeFalse();
        decision.SendToBrain.Should().BeFalse();
        decision.Reason.Should().Be("unchanged_skip");
    }

    [Fact]
    public void Evaluate_UnchangedAfterKeepalive_StoresAndSendsToBrainWhenReady()
    {
        var now = DateTime.UtcNow;
        var sut = new ObservationAdmissionGate
        {
            KeepaliveInterval = TimeSpan.FromSeconds(5)
        };

        sut.Evaluate(true, now); // first observation — sets lastAnalyzed
        var decision = sut.Evaluate(false, now.AddSeconds(6)); // 6s > 5s keepalive AND > 4s brain interval

        decision.StoreObservation.Should().BeTrue();
        decision.SendToBrain.Should().BeTrue();
        decision.Reason.Should().Be("keepalive_brain_eligible");
    }

    [Fact]
    public void Evaluate_UnchangedAfterKeepalive_SnapshotOnlyWhenBrainOnCooldown()
    {
        var now = DateTime.UtcNow;
        var sut = new ObservationAdmissionGate
        {
            KeepaliveInterval = TimeSpan.FromSeconds(5),
            BrainMinInterval = TimeSpan.FromSeconds(10)
        };

        sut.Evaluate(true, now); // first observation — sets lastAnalyzed
        var decision = sut.Evaluate(false, now.AddSeconds(6)); // 6s > 5s keepalive BUT < 10s brain interval

        decision.StoreObservation.Should().BeTrue();
        decision.SendToBrain.Should().BeFalse();
        decision.Reason.Should().Be("keepalive_snapshot");
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var now = DateTime.UtcNow;
        var sut = new ObservationAdmissionGate();

        sut.Evaluate(true, now);
        sut.Reset();
        var decision = sut.Evaluate(false, now.AddSeconds(1));

        decision.StoreObservation.Should().BeTrue();
        decision.SendToBrain.Should().BeTrue();
        decision.Reason.Should().Be("first_observation");
    }
}
