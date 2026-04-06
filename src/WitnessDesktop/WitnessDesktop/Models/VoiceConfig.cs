namespace WitnessDesktop.Models;

public static class VoiceConfig
{
    private static readonly Dictionary<(string Provider, string Gender), string> VoiceMap = new()
    {
        [("gemini", "male")] = "Fenrir",
        [("gemini", "female")] = "Kore",
        [("openai", "male")] = "ash",
        [("openai", "female")] = "shimmer",
    };

    /// <summary>
    /// Resolves a voice name from provider + gender. Falls back to provider default if unknown gender.
    /// When voiceId is provided, it is returned directly, bypassing the gender-based lookup.
    /// </summary>
    public static string GetVoiceName(string provider, string gender, string? voiceId = null)
    {
        if (voiceId is not null)
            return voiceId;
        var key = (provider.ToLowerInvariant(), gender.ToLowerInvariant());
        return VoiceMap.TryGetValue(key, out var voice) ? voice : GetDefault(provider);
    }

    private static string GetDefault(string provider) => provider.ToLowerInvariant() switch
    {
        "openai" => "ash",
        _ => "Fenrir"
    };
}
