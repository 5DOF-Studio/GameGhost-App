using GaimerDesktop.Services.Auth;

namespace GaimerDesktop.Tests.Services.Auth;

/// <summary>
/// Tests for MockAuthService — always-authorized dev/test auth provider.
/// </summary>
public class MockAuthServiceTests
{
    [Fact]
    public async Task ValidateDeviceAsync_ReturnsAuthorized()
    {
        var svc = new MockAuthService();
        var result = await svc.ValidateDeviceAsync();

        result.Authorized.Should().BeTrue();
        result.UserName.Should().Be("Developer");
        result.Reason.Should().BeNull();
    }

    [Fact]
    public async Task IsAuthorized_AfterValidation_IsTrue()
    {
        var svc = new MockAuthService();

        // Before validation
        svc.IsAuthorized.Should().BeFalse();

        await svc.ValidateDeviceAsync();

        // After validation
        svc.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task UserName_AfterValidation_IsDeveloper()
    {
        var svc = new MockAuthService();

        // Before validation
        svc.UserName.Should().BeNull();

        await svc.ValidateDeviceAsync();

        // After validation
        svc.UserName.Should().Be("Developer");
    }

    [Fact]
    public async Task FetchApiKeysAsync_ReturnsNull()
    {
        var svc = new MockAuthService();
        var result = await svc.FetchApiKeysAsync();

        result.Should().BeNull();
    }
}
