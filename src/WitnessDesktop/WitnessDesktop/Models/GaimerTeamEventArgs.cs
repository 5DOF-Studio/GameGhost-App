namespace WitnessDesktop.Models;

public class GaimerTeamResultEventArgs : EventArgs
{
    public required GaimerTeamResult Result { get; init; }
    public string ResponseFormat { get; init; } = "voice";
}

public class GaimerTeamProgressEventArgs : EventArgs
{
    public required string TaskId { get; init; }
    public required string Message { get; init; }
}

public class GaimerTeamPermissionEventArgs : EventArgs
{
    public required GaimerTeamPermissionRequest Request { get; init; }
}
