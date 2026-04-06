using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Tests.Services;

/// <summary>
/// Tests for BrainServiceFactory — verifies centralized brain provider selection
/// based on InferenceMode and local runtime health.
/// </summary>
public class BrainServiceFactoryTests : IDisposable
{
    private readonly SettingsService _settings = new();
    private readonly InferenceProviderPolicy _policy = new();
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        foreach (var d in _disposables) d.Dispose();
    }

    private BrainServiceFactory CreateFactory(ILocalModelRuntime runtime, ITelemetryService? telemetry = null)
        => new(_policy, _settings, runtime, telemetry);

    private IBrainService CreateMockBrain()
    {
        var svc = new MockBrainService(Mock.Of<ILogger<MockBrainService>>());
        _disposables.Add(svc);
        return svc;
    }

    private IBrainService CreateFakeCloudBrain()
    {
        var svc = new FakeBrainService("OpenRouter (google/gemini-2.5-flash)");
        _disposables.Add(svc);
        return svc;
    }

    private IBrainService CreateFakeLocalBrain()
    {
        var svc = new FakeBrainService("Local MiniCPM");
        _disposables.Add(svc);
        return svc;
    }

    // ── CloudOnly ───────────────────────────────────────────────────────────

    [Fact]
    public async Task BrainServiceFactory_CloudOnly_ReturnsCloudBrain()
    {
        _settings.InferenceMode = InferenceMode.CloudOnly;
        var runtime = new MockLocalModelRuntime(available: true, brain: true);
        var factory = CreateFactory(runtime);

        var result = await factory.CreateAsync(
            CreateFakeCloudBrain, CreateFakeLocalBrain, CreateMockBrain,
            cloudBrainAvailable: true);

        result.Brain.ProviderName.Should().Contain("OpenRouter");
        result.Selection.Mode.Should().Be(InferenceMode.CloudOnly);
    }

    // ── LocalOnly ───────────────────────────────────────────────────────────

    [Fact]
    public async Task BrainServiceFactory_LocalOnly_WithHealthyRuntime_ReturnsLocalMiniCpmBrainService()
    {
        _settings.InferenceMode = InferenceMode.LocalOnly;
        var runtime = new MockLocalModelRuntime(available: true, brain: true, voice: true);
        var factory = CreateFactory(runtime);

        var result = await factory.CreateAsync(
            CreateFakeCloudBrain, CreateFakeLocalBrain, CreateMockBrain,
            cloudBrainAvailable: true);

        result.Brain.ProviderName.Should().Be("Local MiniCPM");
        result.Selection.LocalBrainActive.Should().BeTrue();
    }

    [Fact]
    public async Task BrainServiceFactory_LocalOnly_WithUnhealthyRuntime_ReturnsMock()
    {
        _settings.InferenceMode = InferenceMode.LocalOnly;
        var runtime = new MockLocalModelRuntime(available: false);
        var factory = CreateFactory(runtime);

        var result = await factory.CreateAsync(
            CreateFakeCloudBrain, CreateFakeLocalBrain, CreateMockBrain,
            cloudBrainAvailable: true);

        // LocalOnly + unhealthy = no fallback allowed, returns mock
        result.Brain.ProviderName.Should().Be("Mock Brain");
        result.Selection.Available.Should().BeFalse();
        result.Reason.Should().NotBeNullOrEmpty();
    }

    // ── LocalFirst ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BrainServiceFactory_LocalFirst_WithHealthyRuntime_ReturnsLocal()
    {
        _settings.InferenceMode = InferenceMode.LocalFirst;
        var runtime = new MockLocalModelRuntime(available: true, brain: true, voice: true);
        var factory = CreateFactory(runtime);

        var result = await factory.CreateAsync(
            CreateFakeCloudBrain, CreateFakeLocalBrain, CreateMockBrain,
            cloudBrainAvailable: true);

        result.Brain.ProviderName.Should().Be("Local MiniCPM");
    }

    [Fact]
    public async Task BrainServiceFactory_LocalFirst_WithUnhealthyRuntime_FallsBackToCloud()
    {
        _settings.InferenceMode = InferenceMode.LocalFirst;
        var runtime = new MockLocalModelRuntime(available: false);
        var factory = CreateFactory(runtime);

        var result = await factory.CreateAsync(
            CreateFakeCloudBrain, CreateFakeLocalBrain, CreateMockBrain,
            cloudBrainAvailable: true);

        result.Brain.ProviderName.Should().Contain("OpenRouter");
        result.Selection.CloudFallbackActive.Should().BeTrue();
        result.Reason.Should().Contain("fallback");
    }

    // ── Telemetry ───────────────────────────────────────────────────────────

    [Fact]
    public async Task BrainServiceFactory_EmitsTelemetry_OnProviderSelection()
    {
        _settings.InferenceMode = InferenceMode.CloudOnly;
        var runtime = new MockLocalModelRuntime();
        var telemetry = new Mock<ITelemetryService>();
        var factory = CreateFactory(runtime, telemetry.Object);

        await factory.CreateAsync(
            CreateFakeCloudBrain, CreateFakeLocalBrain, CreateMockBrain,
            cloudBrainAvailable: true);

        telemetry.Verify(t => t.TrackEvent(
            "brain", "provider_selected",
            It.Is<Dictionary<string, string>>(d =>
                d["mode"] == "CloudOnly" &&
                d.ContainsKey("provider"))),
            Times.Once);
    }

    // ── Test Double ─────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal IBrainService stub that only exposes ProviderName.
    /// Used to verify factory selection without needing real dependencies.
    /// </summary>
    private sealed class FakeBrainService : IBrainService
    {
        public FakeBrainService(string providerName) => ProviderName = providerName;
        public string ProviderName { get; }
        public bool IsBusy => false;
        public System.Threading.Channels.ChannelReader<BrainResult> Results =>
            System.Threading.Channels.Channel.CreateBounded<BrainResult>(1).Reader;
        public bool TrySubmitFrame(byte[] imageData, string context) => false;
        public Task SubmitImageAsync(byte[] imageData, string context, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task SubmitQueryAsync(string userQuery, SharedContextEnvelope context, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<string> ChatAsync(string userQuery, IReadOnlyList<ChatMessage> chatHistory, CancellationToken ct = default)
            => Task.FromResult("fake reply");
        public void CancelAll() { }
        public void Dispose() { }
    }
}
