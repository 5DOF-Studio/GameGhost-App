using System.Threading.Channels;
using WitnessDesktop.Models.Exchange;

namespace WitnessDesktop.Services;

/// <summary>
/// Priority channel for voice→brain requests. Voice writes when deferring;
/// brain reads with higher priority than regular capture cycle.
/// </summary>
public interface IBrainRequestChannel
{
    /// <summary>Write a brain request (voice side).</summary>
    ValueTask WriteAsync(BrainRequest request, CancellationToken ct = default);

    /// <summary>Try to read a pending request (brain side). Non-blocking.</summary>
    bool TryRead(out BrainRequest? request);

    /// <summary>Channel reader for async enumeration.</summary>
    ChannelReader<BrainRequest> Reader { get; }

    /// <summary>Number of pending requests.</summary>
    int PendingCount { get; }
}
