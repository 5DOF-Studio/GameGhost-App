namespace GaimerDesktop.Services;

/// <summary>
/// Detects meaningful frame changes using perceptual hashing (dHash).
/// Enables the Diff capture precept -- brain only receives frames where
/// the board state has materially changed.
/// </summary>
public interface IFrameDiffService
{
    /// <summary>
    /// Computes a 64-bit dHash perceptual hash of the image.
    /// </summary>
    ulong ComputeHash(byte[] imageData);

    /// <summary>
    /// Computes hash for a region of interest (ROI) within the image.
    /// </summary>
    ulong ComputeHash(byte[] imageData, CropRect roi);

    /// <summary>
    /// Returns the Hamming distance between two hashes (0 = identical).
    /// </summary>
    int CompareHashes(ulong hash1, ulong hash2);

    /// <summary>
    /// Returns true if the frame has changed significantly since last check.
    /// Updates internal state with the new hash. Uses constructor default when threshold is -1.
    /// </summary>
    bool HasChanged(byte[] imageData, int threshold = -1);

    /// <summary>
    /// Event fired when HasChanged detects a significant change (debounced at 1.5s).
    /// </summary>
    event EventHandler<FrameChangeEventArgs>? FrameChanged;
}

public record CropRect(int X, int Y, int Width, int Height);

public class FrameChangeEventArgs : EventArgs
{
    public required int HammingDistance { get; init; }
    public required ulong PreviousHash { get; init; }
    public required ulong CurrentHash { get; init; }
    public required DateTime Timestamp { get; init; }
}
