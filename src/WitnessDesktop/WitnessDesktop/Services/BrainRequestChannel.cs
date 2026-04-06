using System.Threading.Channels;
using WitnessDesktop.Models.Exchange;

namespace WitnessDesktop.Services;

/// <summary>
/// Bounded Channel(8, DropOldest) for voice→brain priority requests.
/// Single-reader (brain), multi-writer (voice grounding, exchange manager).
/// </summary>
public sealed class BrainRequestChannel : IBrainRequestChannel
{
    private readonly Channel<BrainRequest> _channel;

    public BrainRequestChannel()
    {
        _channel = Channel.CreateBounded<BrainRequest>(new BoundedChannelOptions(8)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelReader<BrainRequest> Reader => _channel.Reader;

    // Reader.Count is runtime-maintained and always correct for BoundedChannel —
    // handles DropOldest silently dropping items and ReadAllAsync consuming without TryRead.
    public int PendingCount => _channel.Reader.Count;

    public async ValueTask WriteAsync(BrainRequest request, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(request, ct);
    }

    public bool TryRead(out BrainRequest? request)
    {
        if (_channel.Reader.TryRead(out var item))
        {
            request = item;
            return true;
        }
        request = null;
        return false;
    }
}
