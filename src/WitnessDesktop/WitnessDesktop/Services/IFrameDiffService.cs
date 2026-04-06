namespace WitnessDesktop.Services;

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
    /// Returns the Hamming distance between the current frame and the last observed frame hash
    /// without mutating internal state. Uses default hash width (9 = 64-bit).
    /// </summary>
    int GetDistanceFromLast(byte[] imageData);

    /// <summary>
    /// Returns the Hamming distance using a specified hash grid width without mutating state.
    /// Higher widths detect finer spatial changes (e.g. 33 for chess piece moves).
    /// </summary>
    int GetDistanceFromLast(byte[] imageData, int hashWidth);

    /// <summary>
    /// Returns true if the frame has changed significantly since last check.
    /// Updates internal state with the new hash. Uses constructor default when threshold is -1.
    /// </summary>
    bool HasChanged(byte[] imageData, int threshold = -1);

    /// <summary>
    /// Returns true if the frame has changed, using the specified hash grid width.
    /// Higher widths produce more bits and detect finer spatial changes.
    /// </summary>
    bool HasChanged(byte[] imageData, int threshold, int hashWidth);

    /// <summary>
    /// Reset the internal hash state. Call when starting a new game.
    /// </summary>
    void ResetHash();

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
