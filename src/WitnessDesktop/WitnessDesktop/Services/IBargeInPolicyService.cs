using WitnessDesktop.Models.Exchange;

namespace WitnessDesktop.Services;

public interface IBargeInPolicyService
{
    BargeInPolicy CurrentPolicy { get; }
    bool IsBargeInEnabled { get; }
    void SetEnabled(bool enabled);
    void SetCategoryEnabled(BargeInCategory category, bool enabled);
    bool IsCategoryAllowed(BargeInCategory category);
    event EventHandler<BargeInPolicy>? PolicyChanged;
}
