namespace WitnessDesktop.Models;

/// <summary>
/// Inputs for building a context envelope when the brain does not have its own
/// chat/target store. Caller (e.g. MainViewModel) provides these.
/// </summary>
public sealed class ContextAssemblyInputs
{
    public IReadOnlyList<ChatMessage> RecentChat { get; init; } = [];
    public CaptureTarget? ActiveTarget { get; init; }
}
