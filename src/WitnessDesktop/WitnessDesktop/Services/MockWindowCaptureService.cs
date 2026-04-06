using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public class MockWindowCaptureService : IWindowCaptureService
{
    private Timer? _captureTimer;
    private CaptureEmissionGate _emissionGate = new();
    private long _frameCounter;
    private int _captureIntervalMs = 5000;

    public event EventHandler<byte[]>? FrameCaptured;

    public bool IsCapturing { get; private set; }
    public CaptureTarget? CurrentTarget { get; private set; }

    public Task<IReadOnlyList<CaptureTarget>> GetCaptureTargetsAsync()
    {
        var targets = new List<CaptureTarget>
        {
            new()
            {
                Handle = 1,
                ProcessName = "Google Chrome",
                WindowTitle = "Chess.com - Play Chess Online",
                ChessBadge = "♟️ Chess.com"
            },
            new()
            {
                Handle = 2,
                ProcessName = "Discord",
                WindowTitle = "Gaming Squad - Discord"
            },
            new()
            {
                Handle = 3,
                ProcessName = "Steam",
                WindowTitle = "Steam"
            },
            new()
            {
                Handle = 4,
                ProcessName = "Firefox",
                WindowTitle = "Lichess - Free Online Chess",
                ChessBadge = "♟️ Lichess"
            },
            new()
            {
                Handle = 5,
                ProcessName = "Valorant",
                WindowTitle = "VALORANT"
            }
        };

        return Task.FromResult<IReadOnlyList<CaptureTarget>>(targets);
    }

    public Task StartCaptureAsync(CaptureTarget target, int captureIntervalMs = 5000)
    {
        if (IsCapturing) return Task.CompletedTask;

        CurrentTarget = target;
        IsCapturing = true;
        _frameCounter = 0;
        _captureIntervalMs = captureIntervalMs;
        _emissionGate = new CaptureEmissionGate();
        _captureTimer = new Timer(CaptureFrame, null, 0, _captureIntervalMs);
        return Task.CompletedTask;
    }

    public Task StopCaptureAsync()
    {
        IsCapturing = false;
        CurrentTarget = null;
        _emissionGate.Reset();
        _captureTimer?.Dispose();
        _captureTimer = null;
        return Task.CompletedTask;
    }

    private void CaptureFrame(object? state)
    {
        if (!IsCapturing) return;
        var frame = GeneratePlaceholderImage();
        if (_emissionGate.ShouldEmit(frame))
        {
            FrameCaptured?.Invoke(this, frame);
        }
    }

    /// <summary>
    /// Generates a unique placeholder frame each tick so the change-only
    /// emission gate treats every mock capture as a board change.
    /// </summary>
    private byte[] GeneratePlaceholderImage()
    {
        var counter = Interlocked.Increment(ref _frameCounter);
        return BitConverter.GetBytes(counter);
    }
}
