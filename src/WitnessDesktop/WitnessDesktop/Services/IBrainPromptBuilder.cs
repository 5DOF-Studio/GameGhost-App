using WitnessDesktop.Models;
using WitnessDesktop.Services.Replay;

namespace WitnessDesktop.Services;

/// <summary>
/// Builds structured prompts for the brain image analysis pipeline.
/// Separates system message (identity + context) from user message (image + brief text).
/// </summary>
public interface IBrainPromptBuilder
{
    /// <summary>
    /// Build the system message for brain image analysis.
    /// Assembles identity, capabilities, output format, game context, and previous game sections.
    /// </summary>
    /// <param name="agent">The active agent (provides BrainPersonalityPrefix).</param>
    /// <param name="l1Events">Recent L1 brain events (last 30s).</param>
    /// <param name="journalSummary">Brief summary from IGameJournalService, or null.</param>
    /// <param name="rollingSummary">L2 rolling summary (30s-5min), or null.</param>
    /// <param name="previousGameSummary">Summary of the previous game, or null.</param>
    /// <param name="isConnectedToGame">Whether the brain is currently connected to a live game session.</param>
    string BuildSystemPrompt(Agent agent, IReadOnlyList<BrainEvent> l1Events,
        string? journalSummary, string? rollingSummary, string? previousGameSummary,
        bool isConnectedToGame);

    /// <summary>
    /// Build the user message content (text portion) for image analysis.
    /// Minimal — all context is in the system message.
    /// </summary>
    string BuildUserPrompt(string gameType, int moveNumber);

    /// <summary>
    /// Formats a ReplayContext into a text block suitable for injection into a brain prompt.
    /// Pure formatter — does NOT call the retrieval service itself. The caller decides
    /// when to retrieve replay context and passes the result here.
    /// </summary>
    /// <param name="replayContext">The pre-retrieved replay context to format.</param>
    /// <param name="maxTokenBudget">Approximate token budget (1 token ~ 4 chars). Default 600.</param>
    /// <returns>Formatted text block with chronological replay items.</returns>
    string AssembleReplayContext(ReplayContext replayContext, int maxTokenBudget = 600);
}
