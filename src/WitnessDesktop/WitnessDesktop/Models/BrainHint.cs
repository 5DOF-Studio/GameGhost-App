namespace WitnessDesktop.Models;

public class BrainHint
{
    public string Signal { get; init; } = "none";
    public string Urgency { get; init; } = "low";
    public string Summary { get; init; } = string.Empty;
    public string? SuggestedMove { get; init; }
    public int Evaluation { get; init; }
    public int? EvalDelta { get; init; }
}
