using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public sealed class ObservationAdmissionGate : IObservationAdmissionGate
{
    private DateTime _lastStoredAtUtc = DateTime.MinValue;
    private DateTime _lastAnalyzedAtUtc = DateTime.MinValue;
    private bool _hasSeenFirstObservation;

    public TimeSpan KeepaliveInterval { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan BrainMinInterval { get; init; } = TimeSpan.FromSeconds(4);

    public ObservationAdmissionDecision Evaluate(bool hasMeaningfulChange, DateTime observedAtUtc)
    {
        if (!_hasSeenFirstObservation)
        {
            _hasSeenFirstObservation = true;
            _lastStoredAtUtc = observedAtUtc;
            _lastAnalyzedAtUtc = observedAtUtc;
            return new ObservationAdmissionDecision
            {
                StoreObservation = true,
                SendToBrain = true,
                Reason = "first_observation"
            };
        }

        if (hasMeaningfulChange)
        {
            _lastStoredAtUtc = observedAtUtc;

            if (observedAtUtc - _lastAnalyzedAtUtc >= BrainMinInterval)
            {
                _lastAnalyzedAtUtc = observedAtUtc;
                return new ObservationAdmissionDecision
                {
                    StoreObservation = true,
                    SendToBrain = true,
                    Reason = "changed_and_ready_for_brain"
                };
            }

            return new ObservationAdmissionDecision
            {
                StoreObservation = true,
                SendToBrain = false,
                Reason = "changed_but_brain_cooldown"
            };
        }

        if (observedAtUtc - _lastStoredAtUtc >= KeepaliveInterval)
        {
            _lastStoredAtUtc = observedAtUtc;
            // Time-based brain submission for agents with high diff thresholds (e.g., RASA DiffThreshold=255).
            // Without this, only the first frame ever reaches brain when dHash never triggers "changed".
            var brainReady = observedAtUtc - _lastAnalyzedAtUtc >= BrainMinInterval;
            if (brainReady)
                _lastAnalyzedAtUtc = observedAtUtc;
            return new ObservationAdmissionDecision
            {
                StoreObservation = true,
                SendToBrain = brainReady,
                Reason = brainReady ? "keepalive_brain_eligible" : "keepalive_snapshot"
            };
        }

        return new ObservationAdmissionDecision
        {
            StoreObservation = false,
            SendToBrain = false,
            Reason = "unchanged_skip"
        };
    }

    public void Reset()
    {
        _lastStoredAtUtc = DateTime.MinValue;
        _lastAnalyzedAtUtc = DateTime.MinValue;
        _hasSeenFirstObservation = false;
    }
}
