using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Tests.Services;

public class InferenceProviderPolicyTests
{
    private readonly InferenceProviderPolicy _policy = new();

    private static LocalRuntimeHealth Healthy(bool brain = true, bool voice = true) => new()
    {
        RuntimeAvailable = true,
        BrainAvailable = brain,
        VoiceAvailable = voice,
        RuntimeName = "test-runtime",
        ModelId = "minicpm-o-test"
    };

    private static LocalRuntimeHealth Unhealthy(string reason = "runtime not found") => new()
    {
        RuntimeAvailable = false,
        BrainAvailable = false,
        VoiceAvailable = false,
        FailureReason = reason
    };

    private FakeLocalModelRuntime RuntimeReturning(LocalRuntimeHealth health) => new(health);

    // --- CloudOnly ---

    [Fact]
    public async Task Resolve_CloudOnly_IgnoresLocalRuntimeHealth()
    {
        var runtime = RuntimeReturning(Healthy());

        var selection = await _policy.ResolveAsync(
            InferenceMode.CloudOnly,
            runtime,
            cloudBrainAvailable: true,
            cloudVoiceAvailable: true);

        selection.Mode.Should().Be(InferenceMode.CloudOnly);
        selection.LocalBrainActive.Should().BeFalse();
        selection.LocalVoiceActive.Should().BeFalse();
        selection.CloudFallbackActive.Should().BeFalse();
        selection.Available.Should().BeTrue();
    }

    // --- LocalOnly ---

    [Fact]
    public async Task Resolve_LocalOnly_WithHealthyRuntime_SelectsLocalBrainAndVoice()
    {
        var runtime = RuntimeReturning(Healthy());

        var selection = await _policy.ResolveAsync(
            InferenceMode.LocalOnly,
            runtime,
            cloudBrainAvailable: true,
            cloudVoiceAvailable: true);

        selection.Mode.Should().Be(InferenceMode.LocalOnly);
        selection.LocalBrainActive.Should().BeTrue();
        selection.LocalVoiceActive.Should().BeTrue();
        selection.CloudFallbackActive.Should().BeFalse();
        selection.Available.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_LocalOnly_WithUnhealthyRuntime_ReturnsUnavailableSelection()
    {
        var runtime = RuntimeReturning(Unhealthy());

        var selection = await _policy.ResolveAsync(
            InferenceMode.LocalOnly,
            runtime,
            cloudBrainAvailable: true,
            cloudVoiceAvailable: true);

        selection.Mode.Should().Be(InferenceMode.LocalOnly);
        selection.LocalBrainActive.Should().BeFalse();
        selection.LocalVoiceActive.Should().BeFalse();
        selection.CloudFallbackActive.Should().BeFalse();
        selection.Available.Should().BeFalse();
        selection.FailureReason.Should().NotBeNullOrEmpty();
    }

    // --- LocalFirst ---

    [Fact]
    public async Task Resolve_LocalFirst_WithHealthyRuntime_PrefersLocal()
    {
        var runtime = RuntimeReturning(Healthy());

        var selection = await _policy.ResolveAsync(
            InferenceMode.LocalFirst,
            runtime,
            cloudBrainAvailable: true,
            cloudVoiceAvailable: true);

        selection.Mode.Should().Be(InferenceMode.LocalFirst);
        selection.LocalBrainActive.Should().BeTrue();
        selection.LocalVoiceActive.Should().BeTrue();
        selection.CloudFallbackActive.Should().BeFalse();
        selection.Available.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_LocalFirst_WithUnhealthyRuntime_FallsBackToCloud()
    {
        var runtime = RuntimeReturning(Unhealthy());

        var selection = await _policy.ResolveAsync(
            InferenceMode.LocalFirst,
            runtime,
            cloudBrainAvailable: true,
            cloudVoiceAvailable: true);

        selection.Mode.Should().Be(InferenceMode.LocalFirst);
        selection.LocalBrainActive.Should().BeFalse();
        selection.LocalVoiceActive.Should().BeFalse();
        selection.CloudFallbackActive.Should().BeTrue();
        selection.Available.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_LocalFirst_WithoutCloudSupport_RemainsUnavailable()
    {
        var runtime = RuntimeReturning(Unhealthy());

        var selection = await _policy.ResolveAsync(
            InferenceMode.LocalFirst,
            runtime,
            cloudBrainAvailable: false,
            cloudVoiceAvailable: false);

        selection.Mode.Should().Be(InferenceMode.LocalFirst);
        selection.LocalBrainActive.Should().BeFalse();
        selection.LocalVoiceActive.Should().BeFalse();
        selection.CloudFallbackActive.Should().BeFalse();
        selection.Available.Should().BeFalse();
        selection.FailureReason.Should().NotBeNullOrEmpty();
    }

    // --- Partial capabilities ---

    [Fact]
    public async Task Resolve_LocalFirst_SeparatesBrainAndVoiceAvailability_WhenRuntimeSupportsPartialCapabilities()
    {
        // Runtime has brain but not voice
        var runtime = RuntimeReturning(Healthy(brain: true, voice: false));

        var selection = await _policy.ResolveAsync(
            InferenceMode.LocalFirst,
            runtime,
            cloudBrainAvailable: true,
            cloudVoiceAvailable: true);

        selection.Mode.Should().Be(InferenceMode.LocalFirst);
        selection.LocalBrainActive.Should().BeTrue();
        selection.LocalVoiceActive.Should().BeFalse();
        // Cloud fallback active for the voice portion
        selection.CloudFallbackActive.Should().BeTrue();
        selection.Available.Should().BeTrue();
    }

    // --- Test helper ---

    private sealed class FakeLocalModelRuntime : ILocalModelRuntime
    {
        private readonly LocalRuntimeHealth _health;
        public FakeLocalModelRuntime(LocalRuntimeHealth health) => _health = health;
        public Task<LocalRuntimeHealth> GetHealthAsync(CancellationToken ct = default)
            => Task.FromResult(_health);
    }
}
