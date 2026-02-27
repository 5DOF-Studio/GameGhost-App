namespace WitnessDesktop.Models.Timeline;

public enum ChessEventType
{
    PositionEval = EventOutputType.AgentSpecific + 1,  // Centipawn evaluation from engine
    Tactic           // Best move, fork, pin, skewer, opening patterns
}
