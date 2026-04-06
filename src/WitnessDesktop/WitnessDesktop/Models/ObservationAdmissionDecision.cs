namespace WitnessDesktop.Models;

public sealed record ObservationAdmissionDecision
{
    public required bool StoreObservation { get; init; }
    public required bool SendToBrain { get; init; }
    public required string Reason { get; init; }
}
