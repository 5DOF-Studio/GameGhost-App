using GaimerDesktop.Models;

namespace GaimerDesktop.Services;

public interface IWindowCaptureService
{
    event EventHandler<byte[]>? FrameCaptured;

    Task<IReadOnlyList<CaptureTarget>> GetCaptureTargetsAsync();
    Task StartCaptureAsync(CaptureTarget target, int captureIntervalMs = 30000);
    Task StopCaptureAsync();
    bool IsCapturing { get; }
    CaptureTarget? CurrentTarget { get; }
}

