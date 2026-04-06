namespace WitnessDesktop.Services.Replay;

public interface IReplayAnalysisOrchestrator
{
    void EnqueueSegment(ReplaySegment segment);
    void Start(CancellationToken ct);
    void Stop();
}
