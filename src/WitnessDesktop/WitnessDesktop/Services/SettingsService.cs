#if ANDROID || IOS || MACCATALYST || WINDOWS
#define HAS_MAUI_ESSENTIALS
#endif

using System.Collections.Concurrent;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public sealed class SettingsService : ISettingsService
{
    private const string VoiceProviderKey = "voice_provider";
    private const string VoiceGenderKey = "voice_gender";
    private const string InferenceModeKey = "inference_mode";
    private const string DeviceIdKey = "gaimer_device_id";
    private const string ApiKeyPrefix = "apikey_";
    private const string ClaudeCliPathKey = "claude_cli_path";
    private const string BunPathKey = "bun_path";
    private const string TeamAutoLaunchKey = "team_auto_launch";
    private const string PluginDirPathKey = "plugin_dir_path";
    private const string TeamPermissionModeKey = "team_permission_mode";

    // Thread-safe in-memory fallback for net8.0 library builds (tests) — H1
    private readonly ConcurrentDictionary<string, string> _memStore = new();
    private string? _fallbackDeviceId;

    public event EventHandler<string>? SettingChanged;

    public string VoiceProvider
    {
        get => GetPref(VoiceProviderKey, "gemini");
        set
        {
            if (GetPref(VoiceProviderKey, "gemini") == value) return; // H3
            SetPref(VoiceProviderKey, value);
            SettingChanged?.Invoke(this, nameof(VoiceProvider));
        }
    }

    public string VoiceGender
    {
        get => GetPref(VoiceGenderKey, "male");
        set
        {
            if (GetPref(VoiceGenderKey, "male") == value) return; // H3
            SetPref(VoiceGenderKey, value);
            SettingChanged?.Invoke(this, nameof(VoiceGender));
        }
    }

    public InferenceMode InferenceMode
    {
        get
        {
            var stored = GetPref(InferenceModeKey, nameof(InferenceMode.CloudOnly));
            return Enum.TryParse<InferenceMode>(stored, out var mode) ? mode : InferenceMode.CloudOnly;
        }
        set
        {
            if (GetPref(InferenceModeKey, nameof(InferenceMode.CloudOnly)) == value.ToString()) return; // H3
            SetPref(InferenceModeKey, value.ToString());
            SettingChanged?.Invoke(this, nameof(InferenceMode));
        }
    }

    public string ClaudeCliPath
    {
        get => GetPref(ClaudeCliPathKey, "");
        set
        {
            if (GetPref(ClaudeCliPathKey, "") == value) return; // H3
            SetPref(ClaudeCliPathKey, value);
            SettingChanged?.Invoke(this, nameof(ClaudeCliPath));
        }
    }

    public string BunPath
    {
        get => GetPref(BunPathKey, "");
        set
        {
            if (GetPref(BunPathKey, "") == value) return; // H3
            SetPref(BunPathKey, value);
            SettingChanged?.Invoke(this, nameof(BunPath));
        }
    }

    public bool TeamAutoLaunch
    {
        get => GetPref(TeamAutoLaunchKey, "true") == "true";
        set
        {
            var stringVal = value ? "true" : "false";
            if (GetPref(TeamAutoLaunchKey, "true") == stringVal) return; // H3
            SetPref(TeamAutoLaunchKey, stringVal);
            SettingChanged?.Invoke(this, nameof(TeamAutoLaunch));
        }
    }

    public string PluginDirPath
    {
        get => GetPref(PluginDirPathKey, "");
        set
        {
            if (GetPref(PluginDirPathKey, "") == value) return; // H3
            SetPref(PluginDirPathKey, value);
            SettingChanged?.Invoke(this, nameof(PluginDirPath));
        }
    }

    public string TeamPermissionMode
    {
        get => GetPref(TeamPermissionModeKey, "default");
        set
        {
            if (GetPref(TeamPermissionModeKey, "default") == value) return; // H3 equality guard
            SetPref(TeamPermissionModeKey, value);
            SettingChanged?.Invoke(this, nameof(TeamPermissionMode));
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
