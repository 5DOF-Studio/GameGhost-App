using System.Numerics;
using SkiaSharp;

namespace WitnessDesktop.Services;

/// <summary>
/// Detects meaningful frame changes using the dHash (difference hash) perceptual hashing algorithm.
/// Uses SkiaSharp (MAUI built-in) for image decode and resize -- no additional NuGet packages needed.
///
/// Supports two hash modes:
/// - Standard (width=9): 9x8 → 64-bit hash stored in ulong. Good for scene-level changes.
/// - High-res (width=33): 33x32 → 1024-bit hash stored in byte[]. Detects fine spatial changes
///   like chess piece moves that are invisible at 9x8 resolution.
///
/// Hash width is agent-configurable via CaptureConfig.DiffHashWidth.
/// </summary>
public sealed class FrameDiffService : IFrameDiffService
{
    private ulong _lastHash;
    private byte[]? _lastHighResHash;
    private DateTime _lastChangeTime = DateTime.MinValue;
    private readonly TimeSpan _debounceWindow;
    private readonly int _defaultThreshold;
    private readonly ITelemetryService? _telemetry;

    public FrameDiffService(TimeSpan? debounceWindow = null, int defaultThreshold = 10, ITelemetryService? telemetry = null)
    {
        _debounceWindow = debounceWindow ?? TimeSpan.FromSeconds(1.5);
        _defaultThreshold = defaultThreshold;
        _telemetry = telemetry;
    }

    /// <inheritdoc />
    public void ResetHash()
    {
        _lastHash = 0;
        _lastHighResHash = null;
        _lastChangeTime = DateTime.MinValue;
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

        var subset = new SKRectI(roi.X, roi.Y, roi.X + roi.Width, roi.Y + roi.Height);
        using var cropped = new SKBitmap();
        if (!bitmap.ExtractSubset(cropped, subset))
            return 0;

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
    public int GetDistanceFromLast(byte[] imageData)
    {
        var currentHash = ComputeHash(imageData);
        return CompareHashes(_lastHash, currentHash);
    }

    /// <inheritdoc />
    public int GetDistanceFromLast(byte[] imageData, int hashWidth)
    {
        if (hashWidth <= 9)
            return GetDistanceFromLast(imageData);

        var currentHash = ComputeHighResHash(imageData, hashWidth);
        if (currentHash == null) return 0;
        return _lastHighResHash == null ? currentHash.Length * 8 : HammingDistance(currentHash, _lastHighResHash);
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

                _telemetry?.TrackEvent("capture", "frame_changed", new Dictionary<string, string>
                {
                    ["distance"] = distance.ToString(),
                    ["threshold"] = threshold.ToString()
                });

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

    /// <inheritdoc />
    public bool HasChanged(byte[] imageData, int threshold, int hashWidth)
    {
        if (hashWidth <= 9)
            return HasChanged(imageData, threshold);

        threshold = threshold < 0 ? _defaultThreshold : threshold;
        var currentHash = ComputeHighResHash(imageData, hashWidth);
        if (currentHash == null) return false;

        var distance = _lastHighResHash == null
            ? currentHash.Length * 8  // First frame: max distance → always passes
            : HammingDistance(currentHash, _lastHighResHash);

        if (distance >= threshold)
        {
            var now = DateTime.UtcNow;
            if (now - _lastChangeTime >= _debounceWindow)
            {
                _lastChangeTime = now;
                _lastHighResHash = currentHash;

                _telemetry?.TrackEvent("capture", "frame_changed", new Dictionary<string, string>
                {
                    ["distance"] = distance.ToString(),
                    ["threshold"] = threshold.ToString(),
                    ["hashWidth"] = hashWidth.ToString()
                });

                FrameChanged?.Invoke(this, new FrameChangeEventArgs
                {
                    HammingDistance = distance,
                    PreviousHash = 0,
                    CurrentHash = 0,
                    Timestamp = now
                });
                return true;
            }
        }

        _lastHighResHash = currentHash;
        return false;
    }

    // ── High-res hash (variable-width dHash) ─────────────────────────────────

    /// <summary>
    /// Computes a variable-width dHash. For width=33, height=32, producing a 1024-bit hash
    /// packed into 128 bytes. Same dHash algorithm, just higher spatial resolution.
    /// </summary>
    internal static byte[]? ComputeHighResHash(byte[] imageData, int hashWidth)
    {
        using var bitmap = SKBitmap.Decode(imageData);
        if (bitmap == null) return null;

        int height = hashWidth - 1;
        using var resized = bitmap.Resize(new SKImageInfo(hashWidth, height, SKColorType.Gray8), SKFilterQuality.Low);
        if (resized == null) return null;

        var pixels = resized.GetPixelSpan();
        int totalBits = height * (hashWidth - 1);  // e.g. 32 * 32 = 1024
        var hash = new byte[(totalBits + 7) / 8];

        int bitIndex = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < hashWidth - 1; x++)
            {
                int idx = y * hashWidth + x;
                if (pixels[idx] > pixels[idx + 1])
                    hash[bitIndex / 8] |= (byte)(1 << (bitIndex % 8));
                bitIndex++;
            }
        }

        return hash;
    }

    /// <summary>
    /// Hamming distance between two byte-array hashes.
    /// </summary>
    internal static int HammingDistance(byte[] a, byte[] b)
    {
        int distance = 0;
        int len = Math.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
            distance += BitOperations.PopCount((uint)(a[i] ^ b[i]));
        // Count remaining bytes in the longer array as all-different bits
        for (int i = len; i < Math.Max(a.Length, b.Length); i++)
            distance += 8;
        return distance;
    }
}
