namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Abstraction over native P/Invoke recording calls. Enables unit testing
/// of ReplayRecordingService without native binaries.
/// </summary>
internal interface INativeRecordingBridge
{
    bool StartRecording(uint windowId, string outputPath, int width, int height);
    Task StopRecordingAsync();
    Task RotateSegmentAsync(string newOutputPath);
    int GetStatus(); // 0=idle, 1=recording, 2=error
}
