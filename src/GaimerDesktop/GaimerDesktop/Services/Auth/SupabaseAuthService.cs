using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("GaimerDesktop.Tests")]

namespace GaimerDesktop.Services.Auth;

public sealed class SupabaseAuthService : IAuthService, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _supabaseAnonKey;

    public bool IsAuthorized { get; private set; }
    public string? UserName { get; private set; }

    public SupabaseAuthService(ISettingsService settings)
    {
        _settings = settings;

        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? "";
        _supabaseAnonKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? "";

        _httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(_supabaseAnonKey))
        {
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseAnonKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_supabaseAnonKey}");
        }
    }

    /// <summary>
    /// Internal constructor for unit testing. Accepts an HttpMessageHandler and explicit
    /// Supabase config values so tests don't depend on environment variables.
    /// </summary>
    internal SupabaseAuthService(ISettingsService settings, HttpMessageHandler handler, string supabaseUrl, string supabaseAnonKey)
    {
        _settings = settings;
        _supabaseUrl = supabaseUrl;
        _supabaseAnonKey = supabaseAnonKey;

        _httpClient = new HttpClient(handler);
        if (!string.IsNullOrEmpty(_supabaseAnonKey))
        {
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseAnonKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_supabaseAnonKey}");
        }
    }

    public async Task<AuthResult> ValidateDeviceAsync()
    {
        if (string.IsNullOrEmpty(_supabaseUrl) || string.IsNullOrEmpty(_supabaseAnonKey))
        {
            System.Diagnostics.Debug.WriteLine("[Auth] Supabase not configured — denying access");
            return new AuthResult(false, null, "Supabase not configured");
        }

        try
        {
            var deviceId = await _settings.GetDeviceIdAsync();
            var url = $"{_supabaseUrl.TrimEnd('/')}/functions/v1/validate-device";

            var payload = new { device_id = deviceId };
            var response = await _httpClient.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[Auth] Device validation failed: HTTP {response.StatusCode}");
                return new AuthResult(false, null, $"Server returned {response.StatusCode}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var authorized = json.TryGetProperty("authorized", out var authProp) && authProp.GetBoolean();
            var userName = json.TryGetProperty("user_name", out var nameProp) ? nameProp.GetString() : null;

            IsAuthorized = authorized;
            UserName = userName;

            System.Diagnostics.Debug.WriteLine($"[Auth] Device validation: authorized={authorized}");
            return new AuthResult(authorized, userName, authorized ? null : "Device not in allowed list");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] Device validation error: {ex.Message}");
            return new AuthResult(false, null, ex.Message);
        }
    }

    public async Task<AuthResult> SignInWithEmailAsync(string email, string username)
    {
        if (string.IsNullOrEmpty(_supabaseUrl) || string.IsNullOrEmpty(_supabaseAnonKey))
        {
            // Dev fallback: auto-approve when Supabase isn't configured
            System.Diagnostics.Debug.WriteLine("[Auth] Supabase not configured — dev auto-approve");
            await Task.Delay(1500);
            IsAuthorized = true;
            UserName = username;
            return new AuthResult(true, username, null);
        }

        try
        {
            var deviceId = await _settings.GetDeviceIdAsync();
            var url = $"{_supabaseUrl.TrimEnd('/')}/functions/v1/validate-invite";

            var payload = new { email, username, device_id = deviceId };
            var response = await _httpClient.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[Auth] Invite validation failed: HTTP {response.StatusCode}");
                return new AuthResult(false, null, $"Server returned {response.StatusCode}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var authorized = json.TryGetProperty("authorized", out var authProp) && authProp.GetBoolean();
            var returnedName = json.TryGetProperty("user_name", out var nameProp) ? nameProp.GetString() : username;

            IsAuthorized = authorized;
            UserName = returnedName;

            System.Diagnostics.Debug.WriteLine($"[Auth] Invite validation: authorized={authorized}");
            return new AuthResult(authorized, returnedName, authorized ? null : "Invite not found");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] Invite validation error: {ex.Message}");
            return new AuthResult(false, null, ex.Message);
        }
    }

    public async Task<ApiKeyBundle?> FetchApiKeysAsync()
    {
        if (!IsAuthorized || string.IsNullOrEmpty(_supabaseUrl))
            return null;

        try
        {
            var deviceId = await _settings.GetDeviceIdAsync();
            var url = $"{_supabaseUrl.TrimEnd('/')}/functions/v1/get-api-keys";

            var payload = new { device_id = deviceId };
            var response = await _httpClient.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[Auth] Key fetch failed: HTTP {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return new ApiKeyBundle(
                json.TryGetProperty("gemini_key", out var gk) ? gk.GetString() : null,
                json.TryGetProperty("openai_key", out var ok) ? ok.GetString() : null,
                json.TryGetProperty("openrouter_key", out var ork) ? ork.GetString() : null
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] Key fetch error: {ex.Message}");
            return null;
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
