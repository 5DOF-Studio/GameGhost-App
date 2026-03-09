using System.Threading.Channels;
using GaimerDesktop.Models;

namespace GaimerDesktop.Services;

public interface IBrainEventRouter
{
    void OnScreenCapture(string screenshotRef, TimeSpan gameTime, string method);

    void OnBrainHint(BrainHint hint);

    void OnImageAnalysis(string analysisText);

    void OnDirectMessage(ChatMessage userMsg, ChatMessage brainResponse);

    void OnProactiveAlert(BrainHint hint, string commentary);

    void OnGeneralChat(string text);

    void OnError(string message);

    /// <summary>
    /// Start consuming BrainResult from the brain service's channel.
    /// Call once after IBrainService is initialized. Runs until cancelled.
    /// </summary>
    void StartConsuming(ChannelReader<BrainResult> reader, CancellationToken ct);

    /// <summary>
    /// Stop the channel consumer loop. Safe to call multiple times.
    /// </summary>
    void StopConsuming();
}
