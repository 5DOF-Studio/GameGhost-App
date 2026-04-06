using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public interface IObservationStore
{
    Task<ObservationRecord> StoreAsync(ObservationWriteRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ObservationRecord>> GetRecentAsync(int count = 50, CancellationToken ct = default);
    Task<IReadOnlyList<ObservationRecord>> GetByTimeRangeAsync(string sessionId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
}
