using FluentAssertions;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class BargeInPolicyServiceTests
{
    [Fact]
    public void Default_IsDisabled()
    {
        var sut = new BargeInPolicyService();
        sut.IsBargeInEnabled.Should().BeFalse();
    }

    [Fact]
    public void WhenDisabled_NoCategoryAllowed()
    {
        var sut = new BargeInPolicyService();
        sut.IsCategoryAllowed(BargeInCategory.Reminder).Should().BeFalse();
    }

    [Fact]
    public void SetEnabled_True_EnablesBargeIn()
    {
        var sut = new BargeInPolicyService();
        sut.SetEnabled(true);
        sut.IsBargeInEnabled.Should().BeTrue();
    }

    [Fact]
    public void WhenEnabled_AllCategoriesAllowedByDefault()
    {
        var sut = new BargeInPolicyService();
        sut.SetEnabled(true);
        sut.IsCategoryAllowed(BargeInCategory.Reminder).Should().BeTrue();
        sut.IsCategoryAllowed(BargeInCategory.ToolExecution).Should().BeTrue();
        sut.IsCategoryAllowed(BargeInCategory.CallOut).Should().BeTrue();
        sut.IsCategoryAllowed(BargeInCategory.FreeCommentary).Should().BeTrue();
    }

    [Fact]
    public void SetCategoryEnabled_False_DisablesCategory()
    {
        var sut = new BargeInPolicyService();
        sut.SetEnabled(true);
        sut.SetCategoryEnabled(BargeInCategory.FreeCommentary, false);
        sut.IsCategoryAllowed(BargeInCategory.FreeCommentary).Should().BeFalse();
        sut.IsCategoryAllowed(BargeInCategory.CallOut).Should().BeTrue();
    }

    [Fact]
    public void SetCategoryEnabled_True_ReenablesCategory()
    {
        var sut = new BargeInPolicyService();
        sut.SetEnabled(true);
        sut.SetCategoryEnabled(BargeInCategory.FreeCommentary, false);
        sut.SetCategoryEnabled(BargeInCategory.FreeCommentary, true);
        sut.IsCategoryAllowed(BargeInCategory.FreeCommentary).Should().BeTrue();
    }

    [Fact]
    public void CurrentPolicy_ReflectsState()
    {
        var sut = new BargeInPolicyService();
        sut.SetEnabled(true);
        sut.SetCategoryEnabled(BargeInCategory.Reminder, false);
        var policy = sut.CurrentPolicy;
        policy.IsEnabled.Should().BeTrue();
        policy.AllowedCategories.Should().NotContain(BargeInCategory.Reminder);
    }

    [Fact]
    public void PolicyChanged_FiresOnEnable()
    {
        var sut = new BargeInPolicyService();
        var fired = false;
        sut.PolicyChanged += (_, _) => fired = true;
        sut.SetEnabled(true);
        fired.Should().BeTrue();
    }
}
