using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public class MockWindowCaptureService : IWindowCaptureService
{
    private Timer? _captureTimer;

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

    public Task StartCaptureAsync(CaptureTarget target)
    {
        if (IsCapturing) return Task.CompletedTask;

        CurrentTarget = target;
        IsCapturing = true;
        _captureTimer = new Timer(CaptureFrame, null, 0, 1000);
        return Task.CompletedTask;
    }

    public Task StopCaptureAsync()
    {
        IsCapturing = false;
        CurrentTarget = null;
        _captureTimer?.Dispose();
        _captureTimer = null;
        return Task.CompletedTask;
    }

    private void CaptureFrame(object? state)
    {
        if (!IsCapturing) return;
        FrameCaptured?.Invoke(this, GeneratePlaceholderImage());
    }

    private static byte[] GeneratePlaceholderImage()
    {
        return [];
    }
}

