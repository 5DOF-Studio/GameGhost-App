using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public interface IBrainEventRouter
{
    void OnScreenCapture(string screenshotRef, TimeSpan gameTime, string method);
    
    void OnBrainHint(BrainHint hint);
    
    void OnImageAnalysis(string analysisText);
    
    void OnDirectMessage(ChatMessage userMsg, ChatMessage brainResponse);
    
    void OnProactiveAlert(BrainHint hint, string commentary);

    void OnGeneralChat(string text);
}
