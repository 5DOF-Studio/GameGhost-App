using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Services.Brain;

/// <summary>
/// Centralized brain provider factory. Resolves local vs cloud brain based on
/// InferenceProviderPolicy and current settings. Replaces the hard-coded
/// OpenRouterBrainService construction in MauiProgram.
/// </summary>
public sealed class BrainServiceFactory
{
    private readonly InferenceProviderPolicy _policy;
    private readonly ISettingsService _settings;
    private readonly ILocalModelRuntime _localRuntime;
    private readonly ITelemetryService? _telemetry;

    public BrainServiceFactory(
        InferenceProviderPolicy policy,
        ISettingsService settings,
        ILocalModelRuntime localRuntime,
        ITelemetryService? telemetry = null)
    {
        _policy = policy;
        _settings = settings;
        _localRuntime = localRuntime;
        _telemetry = telemetry;
    }

    /// <summary>
    /// Creates the appropriate IBrainService based on current inference mode and runtime health.
    /// </summary>
    /// <param name="createCloudBrain">Factory delegate for creating the cloud (OpenRouter) brain service.</param>
    /// <param name="createLocalBrain">Factory delegate for creating the local (MiniCPM) brain service.</param>
    /// <param name="createMockBrain">Factory delegate for creating the mock brain service (no API key fallback).</param>
    /// <param name="cloudBrainAvailable">Whether the cloud brain API key is present and valid.</param>
    /// <returns>The resolved IBrainService and the selection that produced it.</returns>
    public async Task<BrainFactoryResult> CreateAsync(
        Func<IBrainService> createCloudBrain,
        Func<IBrainService> createLocalBrain,
        Func<IBrainService> createMockBrain,
        bool cloudBrainAvailable)
    {
        var mode = _settings.InferenceMode;
        Console.WriteLine($"[BrainFactory] Inference mode: {mode}");

        var selection = await _policy.ResolveAsync(
            mode,
            _localRuntime,
            cloudBrainAvailable: cloudBrainAvailable,
            cloudVoiceAvailable: true) // voice availability handled separately by ConversationProviderFactory
            .ConfigureAwait(false);

        Console.WriteLine($"[BrainFactory] Policy result: localBrain={selection.LocalBrainActive}, localVoice={selection.LocalVoiceActive}, cloudFallback={selection.CloudFallbackActive}, available={selection.Available}");
        if (!string.IsNullOrEmpty(selection.FailureReason))
            Console.WriteLine($"[BrainFactory] Failure reason: {selection.FailureReason}");

        IBrainService brain;
        string reason;

        if (selection.LocalBrainActive)
        {
            brain = createLocalBrain();
            reason = "local brain active";
            Console.WriteLine($"[BrainFactory] Mode={mode}, Selection=LocalBrain ({brain.ProviderName})");
        }
        else if (selection.CloudFallbackActive && cloudBrainAvailable)
        {
            brain = createCloudBrain();
            reason = $"cloud fallback — {selection.FailureReason ?? "local unavailable"}";
            Console.WriteLine($"[BrainFactory] Mode={mode}, Selection=CloudFallback ({brain.ProviderName}), Reason={reason}");
        }
        else if (mode == InferenceMode.CloudOnly && cloudBrainAvailable)
        {
            brain = createCloudBrain();
            reason = "cloud-only mode";
            Console.WriteLine($"[BrainFactory] Mode=CloudOnly, Selection=Cloud ({brain.ProviderName})");
        }
        else if (cloudBrainAvailable)
        {
            // Local-only mode but local unavailable, cloud available but not allowed to fall back
            brain = createMockBrain();
            reason = selection.FailureReason ?? "local unavailable, no fallback allowed";
            Console.WriteLine($"[BrainFactory] Mode={mode}, Selection=Mock (local unavailable, no fallback), Reason={reason}");
        }
        else
        {
            brain = createMockBrain();
            reason = "no providers available";
            Console.WriteLine($"[BrainFactory] Mode={mode}, Selection=Mock ({reason})");
        }

        _telemetry?.TrackEvent("brain", "provider_selected", new Dictionary<string, string>
        {
            ["mode"] = mode.ToString(),
            ["provider"] = brain.ProviderName,
            ["localBrainActive"] = selection.LocalBrainActive.ToString(),
            ["cloudFallback"] = selection.CloudFallbackActive.ToString(),
            ["reason"] = reason
        });

        return new BrainFactoryResult(brain, selection, reason);
    }
}

/// <summary>
/// Result of BrainServiceFactory.CreateAsync — carries the resolved brain service,
/// the inference selection, and a human-readable reason for diagnostics.
/// </summary>
public sealed record BrainFactoryResult(
    IBrainService Brain,
    InferenceSelection Selection,
    string Reason);
