using GaimerDesktop.Models;

namespace GaimerDesktop.Services;

/// <summary>
/// Service for building budgeted context envelopes for voice/chat pipelines.
/// Aligned with BRAIN_CONTEXT_PIPELINE_SPEC IBrainContextService contract.
/// </summary>
public interface IBrainContextService
{
    /// <summary>
    /// Builds a context envelope for voice requests.
    /// </summary>
    Task<SharedContextEnvelope> GetContextForVoiceAsync(
        DateTime requestTsUtc,
        string intent = "general",
        int budgetTokens = 900,
        ContextAssemblyInputs? inputs = null,
        CancellationToken ct = default);

    /// <summary>
    /// Builds a context envelope for chat requests.
    /// </summary>
    Task<SharedContextEnvelope> GetContextForChatAsync(
        DateTime requestTsUtc,
        string intent = "general",
        int budgetTokens = 1200,
        ContextAssemblyInputs? inputs = null,
        CancellationToken ct = default);

    /// <summary>
    /// Ingest a brain event into the L1 immediate window.
    /// Called by BrainEventRouter each time a BrainResult is routed.
    /// </summary>
    Task IngestEventAsync(BrainEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Formats an envelope as a text block to prepend to user messages when the provider
    /// does not support explicit context payload. Interim path per GMR-009/GMR-010.
    /// </summary>
    string FormatAsPrefixedContextBlock(SharedContextEnvelope envelope);
}
