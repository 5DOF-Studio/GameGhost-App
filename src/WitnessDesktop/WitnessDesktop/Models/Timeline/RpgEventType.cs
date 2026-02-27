namespace WitnessDesktop.Models.Timeline;

public enum RpgEventType
{
    QuestHint = EventOutputType.AgentSpecific + 1,  // Quest/objective guidance
    Lore,             // World/story context
    ItemAlert         // Notable loot spotted
}
