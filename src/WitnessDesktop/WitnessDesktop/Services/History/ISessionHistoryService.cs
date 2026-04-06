using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Services.History;

/// <summary>
/// Persists session lifecycle, chat messages, and timeline events
/// to the EF Core history database. All methods are fire-and-forget safe:
/// failures log but never propagate exceptions.
/// </summary>
public interface ISessionHistoryService
{
    Task StartSessionAsync(
        string sessionId,
        string? agentKey,
        string? gameType,
        string? connectorName,
        string? targetWindowTitle,
        string? targetProcessName);

    Task FinalizeSessionAsync(string sessionId);

    Task PersistChatMessageAsync(string sessionId, ChatMessage message);

    Task PersistTimelineCheckpointAsync(string sessionId, TimelineCheckpoint checkpoint, int displayOrder);

    Task PersistTimelineEventAsync(string sessionId, TimelineEvent evt, string? checkpointId, int displayOrder);
}
