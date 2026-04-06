namespace WitnessDesktop.Models.Exchange;

/// <summary>
/// Runtime barge-in policy. Controls whether and which categories of unsolicited speech are allowed.
/// Default: disabled, all categories allowed (so enabling the toggle activates everything).
/// </summary>
public sealed class BargeInPolicy
{
    public bool IsEnabled { get; set; }
    public HashSet<BargeInCategory> AllowedCategories { get; set; } = new(Enum.GetValues<BargeInCategory>());

    public bool IsCategoryAllowed(BargeInCategory category)
        => IsEnabled && AllowedCategories.Contains(category);
}
