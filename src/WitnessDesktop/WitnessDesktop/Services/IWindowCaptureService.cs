using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public interface IWindowCaptureService
{
    event EventHandler<byte[]>? FrameCaptured;

    Task<IReadOnlyList<CaptureTarget>> GetCaptureTargetsAsync();
    Task StartCaptureAsync(CaptureTarget target);
    Task StopCaptureAsync();
    bool IsCapturing { get; }
    CaptureTarget? CurrentTarget { get; }
}

