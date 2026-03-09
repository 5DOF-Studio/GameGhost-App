using GaimerDesktop.Models;

namespace GaimerDesktop.Tests.Models;

public class VoiceConfigTests
{
    [Fact]
    public void GetVoiceName_GeminiMale_ReturnsFenrir()
    {
        var result = VoiceConfig.GetVoiceName("gemini", "male");
        result.Should().Be("Fenrir");
    }

    [Fact]
    public void GetVoiceName_GeminiFemale_ReturnsKore()
    {
        var result = VoiceConfig.GetVoiceName("gemini", "female");
        result.Should().Be("Kore");
    }

    [Fact]
    public void GetVoiceName_OpenAIMale_ReturnsAsh()
    {
        var result = VoiceConfig.GetVoiceName("openai", "male");
        result.Should().Be("ash");
    }

    [Fact]
    public void GetVoiceName_OpenAIFemale_ReturnsShimmer()
    {
        var result = VoiceConfig.GetVoiceName("openai", "female");
        result.Should().Be("shimmer");
    }

    [Fact]
    public void GetVoiceName_UnknownProvider_ReturnsFenrirFallback()
    {
        // Unknown provider should fall back to Fenrir (Gemini default)
        var result = VoiceConfig.GetVoiceName("elevenlabs", "male");
        result.Should().Be("Fenrir");
    }

    [Fact]
    public void GetVoiceName_UnknownGender_ReturnsFallbackForProvider()
    {
        // Unknown gender with known provider should fall back to provider default
        var geminiResult = VoiceConfig.GetVoiceName("gemini", "nonbinary");
        geminiResult.Should().Be("Fenrir");

        var openaiResult = VoiceConfig.GetVoiceName("openai", "nonbinary");
        openaiResult.Should().Be("ash");
    }

    [Fact]
    public void GetVoiceName_CaseInsensitive()
    {
        // Provider and gender should be case-insensitive
        VoiceConfig.GetVoiceName("GEMINI", "MALE").Should().Be("Fenrir");
        VoiceConfig.GetVoiceName("Gemini", "Female").Should().Be("Kore");
        VoiceConfig.GetVoiceName("OpenAI", "Male").Should().Be("ash");
        VoiceConfig.GetVoiceName("OPENAI", "FEMALE").Should().Be("shimmer");
    }
}
