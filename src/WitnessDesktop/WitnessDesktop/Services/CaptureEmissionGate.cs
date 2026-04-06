namespace WitnessDesktop.Services;

public sealed class CaptureEmissionGate
{
    private ulong _lastFrameHash;
    private bool _hasEmitted;

    public bool ShouldEmit(byte[] frameData)
    {
        var currentHash = ComputeFnv1A64(frameData);

        if (!_hasEmitted)
        {
            _hasEmitted = true;
            _lastFrameHash = currentHash;
            return true;
        }

        if (currentHash == _lastFrameHash)
            return false;

        _lastFrameHash = currentHash;
        return true;
    }

    public void Reset()
    {
        _hasEmitted = false;
        _lastFrameHash = 0;
    }

    private static ulong ComputeFnv1A64(byte[] data)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        foreach (var b in data)
        {
            hash ^= b;
            hash *= prime;
        }

        return hash;
    }
}
