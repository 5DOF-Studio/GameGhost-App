using System.Net;

namespace GaimerDesktop.Tests.Helpers;

/// <summary>
/// Configurable HttpMessageHandler for mocking HTTP dependencies.
/// Intercepts requests before they reach the network.
/// </summary>
public sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public MockHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }

    /// <summary>
    /// Creates a handler that always returns the given JSON with the given status code.
    /// </summary>
    public static MockHttpHandler FromJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new MockHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        }));
    }
}
