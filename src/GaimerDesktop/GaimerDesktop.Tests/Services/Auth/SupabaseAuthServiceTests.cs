using System.Net;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Auth;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.Services.Auth;

/// <summary>
/// Tests for SupabaseAuthService — device validation and API key fetching.
/// Uses the internal constructor with MockHttpHandler for full HTTP control.
/// </summary>
public class SupabaseAuthServiceTests
{
    private const string TestUrl = "https://test.supabase.co";
    private const string TestAnonKey = "test-anon-key";
    private const string TestDeviceId = "device-abc-123";

    private static Mock<ISettingsService> CreateMockSettings()
    {
        var mock = new Mock<ISettingsService>();
        mock.Setup(s => s.GetDeviceIdAsync()).ReturnsAsync(TestDeviceId);
        return mock;
    }

    private static SupabaseAuthService CreateService(
        MockHttpHandler handler,
        Mock<ISettingsService>? settings = null,
        string url = TestUrl,
        string anonKey = TestAnonKey)
    {
        return new SupabaseAuthService(
            (settings ?? CreateMockSettings()).Object,
            handler,
            url,
            anonKey);
    }

    // ──────────────────────────────────────────────
    // ValidateDeviceAsync — missing config
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ValidateDeviceAsync_MissingConfig_ReturnsUnauthorized()
    {
        var handler = MockHttpHandler.FromJson("{}", HttpStatusCode.OK);
        var service = CreateService(handler, url: "", anonKey: "");

        var result = await service.ValidateDeviceAsync();

        result.Authorized.Should().BeFalse();
        result.Reason.Should().Contain("not configured");
    }

    [Fact]
    public async Task ValidateDeviceAsync_MissingUrl_ReturnsUnauthorized()
    {
        var handler = MockHttpHandler.FromJson("{}", HttpStatusCode.OK);
        var service = CreateService(handler, url: "", anonKey: TestAnonKey);

        var result = await service.ValidateDeviceAsync();

        result.Authorized.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDeviceAsync_MissingAnonKey_ReturnsUnauthorized()
    {
        var handler = MockHttpHandler.FromJson("{}", HttpStatusCode.OK);
        var service = CreateService(handler, url: TestUrl, anonKey: "");

        var result = await service.ValidateDeviceAsync();

        result.Authorized.Should().BeFalse();
    }

    // ──────────────────────────────────────────────
    // ValidateDeviceAsync — success path
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ValidateDeviceAsync_Success_ReturnsAuthorized()
    {
        var json = """{"authorized": true, "user_name": "TestUser"}""";
        var handler = MockHttpHandler.FromJson(json, HttpStatusCode.OK);
        var service = CreateService(handler);

        var result = await service.ValidateDeviceAsync();

        result.Authorized.Should().BeTrue();
        result.UserName.Should().Be("TestUser");
        result.Reason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateDeviceAsync_Success_SetsIsAuthorizedTrue()
    {
        var json = """{"authorized": true, "user_name": "TestUser"}""";
        var handler = MockHttpHandler.FromJson(json, HttpStatusCode.OK);
        var service = CreateService(handler);

        service.IsAuthorized.Should().BeFalse("should start unauthorized");

        await service.ValidateDeviceAsync();

        service.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateDeviceAsync_Success_SetsUserName()
    {
        var json = """{"authorized": true, "user_name": "GamerOne"}""";
        var handler = MockHttpHandler.FromJson(json, HttpStatusCode.OK);
        var service = CreateService(handler);

        service.UserName.Should().BeNull("should start null");

        await service.ValidateDeviceAsync();

        service.UserName.Should().Be("GamerOne");
    }

    // ──────────────────────────────────────────────
    // ValidateDeviceAsync — denied / error paths
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ValidateDeviceAsync_Denied_ReturnsUnauthorized()
    {
        var json = """{"authorized": false}""";
        var handler = MockHttpHandler.FromJson(json, HttpStatusCode.OK);
        var service = CreateService(handler);

        var result = await service.ValidateDeviceAsync();

        result.Authorized.Should().BeFalse();
        result.Reason.Should().Contain("not in allowed list");
    }

    [Fact]
    public async Task ValidateDeviceAsync_HttpError_ReturnsUnauthorized()
    {
        var handler = MockHttpHandler.FromJson("""{"error": "bad"}""", HttpStatusCode.InternalServerError);
        var service = CreateService(handler);

        var result = await service.ValidateDeviceAsync();

        result.Authorized.Should().BeFalse();
        result.Reason.Should().Contain("InternalServerError");
    }

    [Fact]
    public async Task ValidateDeviceAsync_NetworkException_ReturnsUnauthorized()
    {
        var handler = new MockHttpHandler((_, _) =>
            throw new HttpRequestException("Network unreachable"));
        var service = CreateService(handler);

        var result = await service.ValidateDeviceAsync();

        result.Authorized.Should().BeFalse();
        result.Reason.Should().Contain("Network unreachable");
    }

    // ──────────────────────────────────────────────
    // FetchApiKeysAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task FetchApiKeysAsync_NotAuthorized_ReturnsNull()
    {
        var handler = MockHttpHandler.FromJson("{}", HttpStatusCode.OK);
        var service = CreateService(handler);

        // Don't call ValidateDeviceAsync, so IsAuthorized remains false
        var bundle = await service.FetchApiKeysAsync();

        bundle.Should().BeNull();
    }

    [Fact]
    public async Task FetchApiKeysAsync_Authorized_ReturnsBundle()
    {
        // First, authorize the service — route by URL path instead of call order
        var handler = new MockHttpHandler((request, _) =>
        {
            if (request.RequestUri?.PathAndQuery.Contains("/validate-device") == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"authorized": true, "user_name": "TestUser"}""",
                        System.Text.Encoding.UTF8,
                        "application/json")
                });
            }
            else
            {
                // /get-api-keys
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"gemini_key": "gk-123", "openai_key": "ok-456", "openrouter_key": "ork-789"}""",
                        System.Text.Encoding.UTF8,
                        "application/json")
                });
            }
        });

        var service = CreateService(handler);
        await service.ValidateDeviceAsync();

        var bundle = await service.FetchApiKeysAsync();

        bundle.Should().NotBeNull();
        bundle!.GeminiKey.Should().Be("gk-123");
        bundle.OpenAiKey.Should().Be("ok-456");
        bundle.OpenRouterKey.Should().Be("ork-789");
    }

    [Fact]
    public async Task FetchApiKeysAsync_HttpError_ReturnsNull()
    {
        // Route by URL path instead of call order
        var handler = new MockHttpHandler((request, _) =>
        {
            if (request.RequestUri?.PathAndQuery.Contains("/validate-device") == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"authorized": true, "user_name": "TestUser"}""",
                        System.Text.Encoding.UTF8,
                        "application/json")
                });
            }
            else
            {
                // /get-api-keys returns 500
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                });
            }
        });

        var service = CreateService(handler);
        await service.ValidateDeviceAsync();

        var bundle = await service.FetchApiKeysAsync();

        bundle.Should().BeNull();
    }
}
