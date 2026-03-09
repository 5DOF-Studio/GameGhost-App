namespace GaimerDesktop.Services.Auth;

public sealed class MockAuthService : IAuthService
{
    public bool IsAuthorized { get; private set; }
    public string? UserName { get; private set; }

    public Task<AuthResult> ValidateDeviceAsync()
    {
        IsAuthorized = true;
        UserName = "Developer";
        Console.WriteLine("[Auth] MockAuthService — always authorized");
        return Task.FromResult(new AuthResult(true, "Developer", null));
    }

    public async Task<AuthResult> SignInWithEmailAsync(string email, string username)
    {
        await Task.Delay(1500); // Simulate server call
        IsAuthorized = true;
        UserName = username;
        System.Diagnostics.Debug.WriteLine($"[Auth] MockAuthService — signed in as '{username}'");
        return new AuthResult(true, username, null);
    }

    public Task<ApiKeyBundle?> FetchApiKeysAsync()
    {
        // In mock mode, keys come from environment variables (dev workflow)
        return Task.FromResult<ApiKeyBundle?>(null);
    }
}
