using System.Threading.Channels;
using WitnessDesktop.Services;

namespace WitnessDesktop.Services.Replay;

public sealed class ReplayAnalysisOrchestrator : IReplayAnalysisOrchestrator
{
    private readonly IVideoAnalysisTool _tool;
    private readonly ISegmentAnalysisStore _store;
    private readonly IGameSkillPackService _packService;
    private readonly ISessionTraceService? _trace;
    private readonly GeminiVideoClient? _client;

    private readonly Channel<ReplaySegment> _channel = Channel.CreateBounded<ReplaySegment>(
        new BoundedChannelOptions(2)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    public ReplayAnalysisOrchestrator(
        IVideoAnalysisTool tool,
        ISegmentAnalysisStore store,
        IGameSkillPackService packService,
        ISessionTraceService? trace = null,
        GeminiVideoClient? client = null)
    {
        _tool = tool;
        _store = store;
        _packService = packService;
        _trace = trace;
        _client = client;
    }

    public void EnqueueSegment(ReplaySegment segment)
    {
        _channel.Writer.TryWrite(segment);
    }

    public void Start(CancellationToken ct)
    {
        Stop();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _consumeTask = Task.Run(() => ConsumeAsync(_cts.Token));

        // [W1] Cleanup stale Gemini files from crashed sessions
        if (_client != null)
        {
            _ = Task.Run(async () =>
            {
                try { await _client.CleanupStaleFilesAsync(TimeSpan.FromMinutes(10), ct); }
                catch { /* Best-effort */ }
            });
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        // [W3] Timeout-based wait to avoid blocking UI indefinitely
        if (_consumeTask != null)
        {
            try { _consumeTask.Wait(TimeSpan.FromSeconds(5)); } catch { }
        }
        _cts?.Dispose();
        _cts = null;
        _consumeTask = null;
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var segment in _channel.Reader.ReadAllAsync(ct))
            {
                if (_packService.ActivePack is not { } pack) continue;

                // Stage a temp copy so originals can be safely deleted during cleanup.
                // Falls back to original path if copy fails (file already pruned or test env).
                string? stagedPath = null;
                try
                {
                    var analyzeSegment = segment;
                    if (File.Exists(segment.FilePath))
                    {
                        stagedPath = Path.Combine(Path.GetTempPath(), $"gaimer-replay-{Guid.NewGuid():N}.mp4");
                        File.Copy(segment.FilePath, stagedPath, overwrite: true);
                        analyzeSegment = segment with { FilePath = stagedPath };
                    }

                    var result = await _tool.AnalyzeAsync(analyzeSegment, pack, ct);
                    await _store.IngestAsync(result, ct);

                    _trace?.TrackEvent("replay.analysis.completed", new Dictionary<string, string>
                    {
                        ["segmentIndex"] = segment.SegmentIndex.ToString(),
                        ["beatCount"] = result.Beats.Count.ToString()
                    });
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _trace?.TrackError($"Video analysis failed for segment {segment.SegmentIndex}: {ex.Message}",
                        "ReplayAnalysisOrchestrator");
                }
                finally
                {
                    if (stagedPath != null)
                        try { File.Delete(stagedPath); } catch { }
                }
            }
        }
        catch (OperationCanceledException) { /* Expected on stop */ }
    }
}
