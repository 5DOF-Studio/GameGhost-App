using System.Numerics;
using SkiaSharp;

namespace GaimerDesktop.Services;

/// <summary>
/// Detects meaningful frame changes using the dHash (difference hash) perceptual hashing algorithm.
/// Uses SkiaSharp (MAUI built-in) for image decode and resize -- no additional NuGet packages needed.
///
/// Algorithm: Resize to 9x8 grayscale, compare adjacent pixels horizontally,
/// producing a 64-bit hash. Hamming distance between hashes measures visual similarity.
/// </summary>
public sealed class FrameDiffService : IFrameDiffService
{
    private ulong _lastHash;
    private DateTime _lastChangeTime = DateTime.MinValue;
    private readonly TimeSpan _debounceWindow;
    private readonly int _defaultThreshold;

    public FrameDiffService(TimeSpan? debounceWindow = null, int defaultThreshold = 10)
    {
        _debounceWindow = debounceWindow ?? TimeSpan.FromSeconds(1.5);
        _defaultThreshold = defaultThreshold;
    }

    /// <inheritdoc />
    public event EventHandler<FrameChangeEventArgs>? FrameChanged;

    /// <inheritdoc />
    public ulong ComputeHash(byte[] imageData)
    {
        using var bitmap = SKBitmap.Decode(imageData);
        if (bitmap == null) return 0;

        using var resized = bitmap.Resize(new SKImageInfo(9, 8, SKColorType.Gray8), SKFilterQuality.Low);
        if (resized == null) return 0;

        var pixels = resized.GetPixelSpan();
        ulong hash = 0;

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int idx = y * 9 + x;
                if (pixels[idx] > pixels[idx + 1])
                    hash |= 1UL << (y * 8 + x);
            }
        }

        return hash;
    }

    /// <inheritdoc />
    public ulong ComputeHash(byte[] imageData, CropRect roi)
    {
        using var bitmap = SKBitmap.Decode(imageData);
        if (bitmap == null) return 0;

        // Extract region of interest
        var subset = new SKRectI(roi.X, roi.Y, roi.X + roi.Width, roi.Y + roi.Height);
        using var cropped = new SKBitmap();
        if (!bitmap.ExtractSubset(cropped, subset))
            return 0;

        // Same hash computation on cropped region
        using var resized = cropped.Resize(new SKImageInfo(9, 8, SKColorType.Gray8), SKFilterQuality.Low);
        if (resized == null) return 0;

        var pixels = resized.GetPixelSpan();
        ulong hash = 0;

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int idx = y * 9 + x;
                if (pixels[idx] > pixels[idx + 1])
                    hash |= 1UL << (y * 8 + x);
            }
        }

        return hash;
    }

    /// <inheritdoc />
    public int CompareHashes(ulong hash1, ulong hash2)
    {
        return BitOperations.PopCount(hash1 ^ hash2);
    }

    /// <inheritdoc />
    public bool HasChanged(byte[] imageData, int threshold = -1)
    {
        threshold = threshold < 0 ? _defaultThreshold : threshold;
        var currentHash = ComputeHash(imageData);
        var distance = CompareHashes(_lastHash, currentHash);

        if (distance >= threshold)
        {
            var now = DateTime.UtcNow;
            if (now - _lastChangeTime >= _debounceWindow)
            {
                _lastChangeTime = now;
                var previousHash = _lastHash;
                _lastHash = currentHash;

                FrameChanged?.Invoke(this, new FrameChangeEventArgs
                {
                    HammingDistance = distance,
                    PreviousHash = previousHash,
                    CurrentHash = currentHash,
                    Timestamp = now
                });
                return true;
            }
        }

        _lastHash = currentHash;
        return false;
    }
}
