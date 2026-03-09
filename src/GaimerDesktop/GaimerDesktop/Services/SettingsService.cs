#if ANDROID || IOS || MACCATALYST || WINDOWS
#define HAS_MAUI_ESSENTIALS
#endif

namespace GaimerDesktop.Services;

public sealed class SettingsService : ISettingsService
{
    private const string VoiceProviderKey = "voice_provider";
    private const string VoiceGenderKey = "voice_gender";
    private const string DeviceIdKey = "gaimer_device_id";
    private const string ApiKeyPrefix = "apikey_";

    // In-memory fallback for net8.0 library builds (tests)
    private readonly Dictionary<string, string> _memStore = new();
    private string? _fallbackDeviceId;

    public event EventHandler<string>? SettingChanged;

    public string VoiceProvider
    {
        get => GetPref(VoiceProviderKey, "gemini");
        set
        {
            SetPref(VoiceProviderKey, value);
            SettingChanged?.Invoke(this, nameof(VoiceProvider));
        }
    }

    public string VoiceGender
    {
        get => GetPref(VoiceGenderKey, "male");
        set
        {
            SetPref(VoiceGenderKey, value);
            SettingChanged?.Invoke(this, nameof(VoiceGender));
        }
    }

    public async Task<string?> GetApiKeyAsync(string provider)
    {
        try
        {
#if HAS_MAUI_ESSENTIALS
            return await SecureStorage.Default.GetAsync($"{ApiKeyPrefix}{provider}");
#else
            await Task.CompletedTask;
            return _memStore.TryGetValue($"{ApiKeyPrefix}{provider}", out var v) ? v : null;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SecureStorage read failed for {provider}: {ex.Message}");
            return null;
        }
    }

    public async Task SetApiKeyAsync(string provider, string key)
    {
        try
        {
#if HAS_MAUI_ESSENTIALS
            await SecureStorage.Default.SetAsync($"{ApiKeyPrefix}{provider}", key);
#else
            await Task.CompletedTask;
            _memStore[$"{ApiKeyPrefix}{provider}"] = key;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SecureStorage write failed for {provider}: {ex.Message}");
        }
    }

    public async Task<string> GetDeviceIdAsync()
    {
        try
        {
#if HAS_MAUI_ESSENTIALS
            var existing = await SecureStorage.Default.GetAsync(DeviceIdKey);
            if (!string.IsNullOrEmpty(existing))
                return existing;

            var newId = Guid.NewGuid().ToString("N");
            await SecureStorage.Default.SetAsync(DeviceIdKey, newId);
            return newId;
#else
            await Task.CompletedTask;
            if (_memStore.TryGetValue(DeviceIdKey, out var id))
                return id;
            var newId = Guid.NewGuid().ToString("N");
            _memStore[DeviceIdKey] = newId;
            return newId;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] DeviceId generation failed: {ex.Message}");
            _fallbackDeviceId ??= Guid.NewGuid().ToString("N");
            return _fallbackDeviceId;
        }
    }

    private string GetPref(string key, string defaultValue)
    {
#if HAS_MAUI_ESSENTIALS
        return Preferences.Default.Get(key, defaultValue);
#else
        return _memStore.TryGetValue(key, out var v) ? v : defaultValue;
#endif
    }

    private void SetPref(string key, string value)
    {
#if HAS_MAUI_ESSENTIALS
        Preferences.Default.Set(key, value);
#else
        _memStore[key] = value;
#endif
    }
}
