namespace GaimerDesktop.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> ValidateDeviceAsync();
    Task<AuthResult> SignInWithEmailAsync(string email, string username);
    Task<ApiKeyBundle?> FetchApiKeysAsync();
    bool IsAuthorized { get; }
    string? UserName { get; }
}

public record AuthResult(bool Authorized, string? UserName, string? Reason);

public record ApiKeyBundle(string? GeminiKey, string? OpenAiKey, string? OpenRouterKey);
