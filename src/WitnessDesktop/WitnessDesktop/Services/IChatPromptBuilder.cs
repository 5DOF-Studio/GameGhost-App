using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public interface IChatPromptBuilder
{
    string BuildSystemPrompt(SessionContext session, IReadOnlyList<ToolDefinition> tools);
    
    string BuildSessionContextBlock(SessionContext session);
}
