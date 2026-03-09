using GaimerDesktop.Models;

namespace GaimerDesktop.Services;

public interface IChatPromptBuilder
{
    string BuildSystemPrompt(SessionContext session, IReadOnlyList<ToolDefinition> tools, Agent? agent = null);

    string BuildSessionContextBlock(SessionContext session);
}
