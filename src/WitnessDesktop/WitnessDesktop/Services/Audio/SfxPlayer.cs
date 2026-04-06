#if MACCATALYST
using AVFoundation;
using Foundation;
#endif

namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Lightweight SFX player using platform-native one-shot playback.
/// Separate audio path from voice — plays concurrently with agent speech.
/// </summary>
public sealed class SfxPlayer : ISfxPlayer
{
#if MACCATALYST
    private AVAudioPlayer? _player;
    private readonly object _lock = new();

    public async Task PlayAsync(string fileName, float volume = 0.25f)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            if (stream == null) return;

            var data = NSData.FromStream(stream);
            if (data == null) return;

            lock (_lock)
            {
                // Stop any previous SFX still playing
                _player?.Stop();
                _player?.Dispose();

                _player = AVAudioPlayer.FromData(data, out var error);
                if (_player == null || error != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SFX] Failed to create player: {error?.Description}");
                    return;
                }

                _player.Volume = volume;
                _player.NumberOfLoops = 0; // Play once
                _player.PrepareToPlay();
                _player.Play();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SFX] PlayAsync failed: {ex.Message}");
        }
    }
#else
    public Task PlayAsync(string fileName, float volume = 0.25f)
    {
        // Windows/other platforms: stub for now
        System.Diagnostics.Debug.WriteLine($"[SFX] PlayAsync not implemented on this platform: {fileName}");
        return Task.CompletedTask;
    }
#endif
}
