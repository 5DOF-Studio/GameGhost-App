#if ANDROID || IOS || MACCATALYST || WINDOWS
#define HAS_MAUI_ESSENTIALS
#endif

using WitnessDesktop.Models.Exchange;

namespace WitnessDesktop.Services;

public sealed class BargeInPolicyService : IBargeInPolicyService
{
    private const string PrefKeyEnabled = "barge_in_enabled";
    private readonly BargeInPolicy _policy = new();

    // In-memory fallback for net8.0 library builds (tests)
    private readonly Dictionary<string, bool> _memStore = new();

    public BargeInPolicyService()
    {
        _policy.IsEnabled = GetBoolPref(PrefKeyEnabled, false);

        foreach (var cat in Enum.GetValues<BargeInCategory>())
        {
            var key = $"barge_in_cat_{cat}";
            if (!GetBoolPref(key, true))
                _policy.AllowedCategories.Remove(cat);
        }
    }

    public BargeInPolicy CurrentPolicy => _policy;
    public bool IsBargeInEnabled => _policy.IsEnabled;

    public void SetEnabled(bool enabled)
    {
        _policy.IsEnabled = enabled;
        SetBoolPref(PrefKeyEnabled, enabled);
        PolicyChanged?.Invoke(this, _policy);
    }

    public void SetCategoryEnabled(BargeInCategory category, bool enabled)
    {
        if (enabled) _policy.AllowedCategories.Add(category);
        else _policy.AllowedCategories.Remove(category);
        SetBoolPref($"barge_in_cat_{category}", enabled);
        PolicyChanged?.Invoke(this, _policy);
    }

    public bool IsCategoryAllowed(BargeInCategory category) => _policy.IsCategoryAllowed(category);
    public event EventHandler<BargeInPolicy>? PolicyChanged;

    private bool GetBoolPref(string key, bool defaultValue)
    {
#if HAS_MAUI_ESSENTIALS
        return Preferences.Default.Get(key, defaultValue);
#else
        return _memStore.TryGetValue(key, out var v) ? v : defaultValue;
#endif
    }

    private void SetBoolPref(string key, bool value)
    {
#if HAS_MAUI_ESSENTIALS
        Preferences.Default.Set(key, value);
#else
        _memStore[key] = value;
#endif
    }
}
