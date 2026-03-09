using System.Threading.Channels;
using GaimerDesktop.Models;

namespace GaimerDesktop.Services;

/// <summary>
/// Brain pipeline service: accepts images and queries, produces BrainResults on a Channel.
/// Runs independently from voice (IConversationProvider) on background threads.
/// BrainEventRouter reads from Results to route to Timeline, Voice, and Ghost Mode.
/// </summary>
public interface IBrainService : IDisposable
{
    /// <summary>
    /// Submit a captured image for brain analysis.
    /// Returns immediately; result appears on the Results channel asynchronously.
    /// </summary>
    /// <param name="imageData">PNG image bytes from screen capture.</param>
    /// <param name="context">Current game state description for the analysis prompt.</param>
    /// <param name="ct">Cancellation token linked to session CTS.</param>
    Task SubmitImageAsync(byte[] imageData, string context, CancellationToken ct = default);

    /// <summary>
    /// Submit a text query with full context envelope for brain processing.
    /// Returns immediately; result appears on the Results channel asynchronously.
    /// </summary>
    /// <param name="userQuery">The user's question or command.</param>
    /// <param name="context">Budgeted context envelope from IBrainContextService.</param>
    /// <param name="ct">Cancellation token linked to session CTS.</param>
    Task SubmitQueryAsync(string userQuery, SharedContextEnvelope context, CancellationToken ct = default);

    /// <summary>
    /// Channel reader for consuming brain results. BrainEventRouter reads from this.
    /// Bounded(32) with DropOldest policy to prevent backpressure from blocking analysis.
    /// </summary>
    ChannelReader<BrainResult> Results { get; }

    /// <summary>
    /// Direct request-reply chat with the brain. Returns the assistant's response text.
    /// Use for out-game text chat where the response should appear in ChatMessages,
    /// not routed through the Channel/BrainEventRouter pipeline.
    /// </summary>
    /// <param name="userQuery">The user's message.</param>
    /// <param name="chatHistory">Recent chat messages for conversational context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assistant's reply text.</returns>
    Task<string> ChatAsync(string userQuery, IReadOnlyList<ChatMessage> chatHistory, CancellationToken ct = default);

    /// <summary>
    /// Cancel all in-flight brain requests (new image submitted, session ended, etc.).
    /// Creates a fresh CancellationTokenSource for subsequent requests.
    /// </summary>
    void CancelAll();

    /// <summary>
    /// True if any brain request is currently in progress.
    /// Tracked with Interlocked.Increment/Decrement for thread safety.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// Name of the backend for logging/diagnostics (e.g., "OpenRouter", "Mock Brain").
    /// </summary>
    string ProviderName { get; }
}
