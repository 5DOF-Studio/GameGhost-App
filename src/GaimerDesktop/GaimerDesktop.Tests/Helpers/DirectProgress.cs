namespace GaimerDesktop.Tests.Helpers;

/// <summary>
/// Simple IProgress&lt;T&gt; that invokes callback directly without SynchronizationContext.
/// Use in tests to avoid Progress&lt;T&gt; posting to captured SynchronizationContext.
/// </summary>
public sealed class DirectProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public DirectProgress(Action<T> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Report(T value) => _handler(value);
}
