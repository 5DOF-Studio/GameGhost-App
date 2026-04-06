using System.Runtime.InteropServices;
using WitnessDesktop.Services.Replay;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// Mac Catalyst implementation of INativeRecordingBridge.
/// Wraps P/Invoke calls to GaimerScreenCapture.xcframework with proper
/// GCHandle pinning and TaskCompletionSource for async callbacks.
/// </summary>
internal sealed class NativeRecordingBridge : INativeRecordingBridge
{
    public bool StartRecording(uint windowId, string outputPath, int width, int height)
    {
        return NativeMethods.sck_start_recording(windowId, outputPath, width, height);
    }

    public async Task StopRecordingAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = new NativeMethods.SckRecordingStoppedCallback(() => tcs.TrySetResult());
        var handle = GCHandle.Alloc(callback);
        try
        {
            NativeMethods.sck_stop_recording(callback);
        }
        catch
        {
            handle.Free();
            throw;
        }

        // W1: 10s timeout prevents indefinite hang if native never calls back.
        // On normal completion, free the handle. On timeout, intentionally leak
        // the handle (32 bytes) to prevent crash if native calls back late.
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(10_000));
        if (completed == tcs.Task)
            handle.Free();
    }

    public async Task RotateSegmentAsync(string newOutputPath)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = new NativeMethods.SckRotationCompletedCallback(() => tcs.TrySetResult());
        var handle = GCHandle.Alloc(callback);
        try
        {
            NativeMethods.sck_rotate_segment(newOutputPath, callback);
        }
        catch
        {
            handle.Free();
            throw;
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(10_000));
        if (completed == tcs.Task)
            handle.Free();
    }

    public int GetStatus()
    {
        return NativeMethods.sck_recording_status();
    }
}
