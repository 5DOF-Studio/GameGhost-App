namespace GaimerDesktop.Services;

public interface ISettingsService
{
    /// <summary>Voice provider: "gemini" or "openai".</summary>
    string VoiceProvider { get; set; }

    /// <summary>Voice gender: "male" or "female".</summary>
    string VoiceGender { get; set; }

    /// <summary>Get an API key from secure storage (Keychain-backed).</summary>
    Task<string?> GetApiKeyAsync(string provider);

    /// <summary>Store an API key in secure storage.</summary>
    Task SetApiKeyAsync(string provider, string key);

    /// <summary>Get or generate a stable device identity.</summary>
    Task<string> GetDeviceIdAsync();

    /// <summary>Fires when any setting changes. Arg is the setting name.</summary>
    event EventHandler<string>? SettingChanged;
}
