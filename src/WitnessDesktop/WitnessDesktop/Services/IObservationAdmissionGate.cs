using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public interface IObservationAdmissionGate
{
    ObservationAdmissionDecision Evaluate(bool hasMeaningfulChange, DateTime observedAtUtc);
    void Reset();
}
