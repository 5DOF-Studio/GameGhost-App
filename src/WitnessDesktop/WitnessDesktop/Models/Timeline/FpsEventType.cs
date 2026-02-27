namespace WitnessDesktop.Models.Timeline;

public enum FpsEventType
{
    ThreatDetected = EventOutputType.AgentSpecific + 1,
    CoverAdvice,
    LoadoutTip,
    ObjectiveUpdate,
    RotationCall
}
