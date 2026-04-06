namespace WitnessDesktop.Models.Exchange;

/// <summary>
/// Categories of unsolicited agent speech when barge-in is enabled (spec Section 9.3).
/// </summary>
public enum BargeInCategory
{
    /// <summary>Deferred result from a prior exchange.</summary>
    Reminder,
    /// <summary>Tool execution progress/result (e.g., "Searching the internet").</summary>
    ToolExecution,
    /// <summary>Situational call-out (danger, opportunity, mate threat).</summary>
    CallOut,
    /// <summary>Unsolicited personality-driven commentary. Lowest priority.</summary>
    FreeCommentary,
}
