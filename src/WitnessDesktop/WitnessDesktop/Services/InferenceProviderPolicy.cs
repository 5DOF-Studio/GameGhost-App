using WitnessDesktop.Models;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Services;

/// <summary>
/// Deterministic policy for resolving local vs cloud inference strategy.
/// Queries local runtime health before selecting providers.
/// </summary>
public sealed class InferenceProviderPolicy
{
    public async Task<InferenceSelection> ResolveAsync(
        InferenceMode mode,
        ILocalModelRuntime localRuntime,
        bool cloudBrainAvailable,
        bool cloudVoiceAvailable,
        CancellationToken ct = default)
    {
        return mode switch
        {
            InferenceMode.CloudOnly => ResolveCloudOnly(cloudBrainAvailable, cloudVoiceAvailable),
            InferenceMode.LocalOnly => await ResolveLocalOnly(localRuntime, ct).ConfigureAwait(false),
            InferenceMode.LocalFirst => await ResolveLocalFirst(localRuntime, cloudBrainAvailable, cloudVoiceAvailable, ct).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown inference mode")
        };
    }

    private static InferenceSelection ResolveCloudOnly(bool cloudBrain, bool cloudVoice)
    {
        var available = cloudBrain || cloudVoice;
        return new InferenceSelection
        {
            Mode = InferenceMode.CloudOnly,
            LocalBrainActive = false,
            LocalVoiceActive = false,
            CloudFallbackActive = false,
            Available = available,
            FailureReason = available ? null : "No cloud providers available"
        };
    }

    private static async Task<InferenceSelection> ResolveLocalOnly(
        ILocalModelRuntime runtime, CancellationToken ct)
    {
        var health = await runtime.GetHealthAsync(ct).ConfigureAwait(false);

        if (!health.RuntimeAvailable)
        {
            return new InferenceSelection
            {
                Mode = InferenceMode.LocalOnly,
                LocalBrainActive = false,
                LocalVoiceActive = false,
                CloudFallbackActive = false,
                Available = false,
                FailureReason = health.FailureReason ?? "Local runtime unavailable"
            };
        }

        return new InferenceSelection
        {
            Mode = InferenceMode.LocalOnly,
            LocalBrainActive = health.BrainAvailable,
            LocalVoiceActive = health.VoiceAvailable,
            CloudFallbackActive = false,
            Available = health.BrainAvailable || health.VoiceAvailable
        };
    }

    private static async Task<InferenceSelection> ResolveLocalFirst(
        ILocalModelRuntime runtime,
        bool cloudBrainAvailable,
        bool cloudVoiceAvailable,
        CancellationToken ct)
    {
        var health = await runtime.GetHealthAsync(ct).ConfigureAwait(false);

        var localBrain = health.RuntimeAvailable && health.BrainAvailable;
        var localVoice = health.RuntimeAvailable && health.VoiceAvailable;

        // Cloud fallback needed when local can't cover a capability but cloud can
        var needsBrainFallback = !localBrain && cloudBrainAvailable;
        var needsVoiceFallback = !localVoice && cloudVoiceAvailable;
        var cloudFallback = needsBrainFallback || needsVoiceFallback;

        var available = localBrain || localVoice || cloudFallback;

        return new InferenceSelection
        {
            Mode = InferenceMode.LocalFirst,
            LocalBrainActive = localBrain,
            LocalVoiceActive = localVoice,
            CloudFallbackActive = cloudFallback,
            Available = available,
            FailureReason = available ? null : "Local runtime unavailable and no cloud fallback"
        };
    }
}
