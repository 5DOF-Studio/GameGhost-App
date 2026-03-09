using GaimerDesktop.Services;

namespace GaimerDesktop.Tests.Services;

/// <summary>
/// Tests for SettingsService under net8.0 (in-memory Dictionary fallback path).
/// No MAUI Essentials available, so Preferences and SecureStorage are replaced by _memStore.
/// </summary>
public class SettingsServiceTests
{
    [Fact]
    public void VoiceProvider_Default_IsGemini()
    {
        var svc = new SettingsService();
        svc.VoiceProvider.Should().Be("gemini");
    }

    [Fact]
    public void VoiceProvider_SetGet_RoundTrips()
    {
        var svc = new SettingsService();
        svc.VoiceProvider = "openai";
        svc.VoiceProvider.Should().Be("openai");
    }

    [Fact]
    public void VoiceProvider_Set_FiresSettingChangedEvent()
    {
        var svc = new SettingsService();
        string? changedName = null;
        svc.SettingChanged += (_, name) => changedName = name;

        svc.VoiceProvider = "openai";

        changedName.Should().Be("VoiceProvider");
    }

    [Fact]
    public void VoiceGender_Default_IsMale()
    {
        var svc = new SettingsService();
        svc.VoiceGender.Should().Be("male");
    }

    [Fact]
    public void VoiceGender_SetGet_RoundTrips()
    {
        var svc = new SettingsService();
        svc.VoiceGender = "female";
        svc.VoiceGender.Should().Be("female");
    }

    [Fact]
    public void VoiceGender_Set_FiresSettingChangedEvent()
    {
        var svc = new SettingsService();
        string? changedName = null;
        svc.SettingChanged += (_, name) => changedName = name;

        svc.VoiceGender = "female";

        changedName.Should().Be("VoiceGender");
    }

    [Fact]
    public async Task GetApiKeyAsync_MissingKey_ReturnsNull()
    {
        var svc = new SettingsService();
        var result = await svc.GetApiKeyAsync("nonexistent_provider");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetApiKeyAsync_GetApiKeyAsync_RoundTrips()
    {
        var svc = new SettingsService();
        await svc.SetApiKeyAsync("openai", "sk-test-key-12345");

        var result = await svc.GetApiKeyAsync("openai");
        result.Should().Be("sk-test-key-12345");
    }

    [Fact]
    public async Task GetDeviceIdAsync_FirstCall_GeneratesGuid()
    {
        var svc = new SettingsService();
        var deviceId = await svc.GetDeviceIdAsync();

        // Device ID should be a GUID in "N" format (32 hex chars, no dashes)
        deviceId.Should().NotBeNullOrEmpty();
        deviceId.Should().HaveLength(32);
        deviceId.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task GetDeviceIdAsync_SecondCall_ReturnsSameValue()
    {
        var svc = new SettingsService();
        var first = await svc.GetDeviceIdAsync();
        var second = await svc.GetDeviceIdAsync();

        first.Should().Be(second);
    }
}
