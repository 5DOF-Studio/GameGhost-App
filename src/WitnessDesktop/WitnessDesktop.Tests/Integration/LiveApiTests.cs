using System.Text.Json;
using System.Net.WebSockets;
using Xunit.Abstractions;
using WitnessDesktop.Models;
using Microsoft.Extensions.Configuration;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Brain;

namespace WitnessDesktop.Tests.Integration;

[Trait("Category", "LiveApi")]
public class LiveApiTests
{
    private readonly ITestOutputHelper _output;

    public LiveApiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GeminiLive_ConnectHandshake_Completes()
    {
        if (!TryBuildGeminiConfig(out var config))
        {
            _output.WriteLine("SKIPPED: GEMINI_APIKEY not set — set env var to run this test");
            return;
        }

        using var service = new GeminiLiveService(config);
        string? error = null;
        service.ErrorOccurred += (_, message) => error = message;

        try
        {
            await service.ConnectAsync(Agents.Chess);
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"SKIPPED: Gemini Live API unreachable — {ex.Message}");
            return;
        }
        catch (WebSocketException ex)
        {
            _output.WriteLine($"Gemini Live WebSocket failure: {ex.Message}");
            throw;
        }

        _output.WriteLine($"Gemini state after connect: {service.State}");
        error.Should().BeNullOrEmpty();
        service.IsConnected.Should().BeTrue();

        await service.DisconnectAsync();
    }

    [Fact]
    public async Task GeminiLive_MinimalPrompt_ConnectsAndReturnsOutput()
    {
        if (!TryBuildGeminiConfig(out var config))
        {
            _output.WriteLine("SKIPPED: GEMINI_APIKEY not set — set env var to run this test");
            return;
        }

        var minimalAgent = new Agent
        {
            Key = "test",
            Id = "test",
            Name = "Test",
            PrimaryGame = "Test",
            IconImage = "test.png",
            PortraitImage = "test.png",
            Description = "Test agent",
            Features = new List<string>(),
            SystemInstruction = "Reply with exactly OK.",
            Type = AgentType.General
        };

        using var service = new GeminiLiveService(config);
        try
        {
            await service.ConnectAsync(minimalAgent);
            service.IsConnected.Should().BeTrue();

            var outcome = await WaitForLiveOutputAsync(
                h => service.TextReceived += h,
                h => service.TextReceived -= h,
                h => service.AudioReceived += h,
                h => service.AudioReceived -= h,
                h => service.ErrorOccurred += h,
                h => service.ErrorOccurred -= h,
                () => service.SendTextAsync("Reply with exactly OK."),
                TimeSpan.FromSeconds(20));

            _output.WriteLine($"Gemini minimal outcome: {outcome}");
            (outcome.StartsWith("text:", StringComparison.OrdinalIgnoreCase) ||
             outcome.StartsWith("audio:", StringComparison.OrdinalIgnoreCase))
                .Should().BeTrue("Gemini should emit either text or audio for a simple prompt");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"SKIPPED: Gemini Live API unreachable — {ex.Message}");
            return;
        }
        catch (WebSocketException ex)
        {
            _output.WriteLine($"Gemini Live WebSocket failure: {ex.Message}");
            throw;
        }
        finally
        {
            await service.DisconnectAsync();
        }
    }

    [Fact]
    public async Task GeminiLive_TextPrompt_ReturnsTextOrAudio()
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_APIKEY")
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _output.WriteLine("SKIPPED: GEMINI_APIKEY not set — set env var to run this test");
            return;
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GEMINI_APIKEY"] = apiKey
            })
            .Build();

        using var service = new GeminiLiveService(config);
        try
        {
            await service.ConnectAsync(Agents.Chess);
            var outcome = await WaitForLiveOutputAsync(
                h => service.TextReceived += h,
                h => service.TextReceived -= h,
                h => service.AudioReceived += h,
                h => service.AudioReceived -= h,
                h => service.ErrorOccurred += h,
                h => service.ErrorOccurred -= h,
                () => service.SendTextAsync("Reply with exactly OK."),
                TimeSpan.FromSeconds(20));

            _output.WriteLine($"Gemini outcome: {outcome}");
            (outcome.StartsWith("text:", StringComparison.OrdinalIgnoreCase) ||
             outcome.StartsWith("audio:", StringComparison.OrdinalIgnoreCase))
                .Should().BeTrue("Gemini should emit either text or audio for a simple prompt");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"SKIPPED: Gemini Live API unreachable — {ex.Message}");
            return;
        }
        catch (WebSocketException ex)
        {
            _output.WriteLine($"Gemini Live WebSocket failure: {ex.Message}");
            throw;
        }
        finally
        {
            await service.DisconnectAsync();
        }
    }

    [Fact]
    public async Task OpenAIRealtime_TextPrompt_ReturnsTextOrAudio()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_APIKEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _output.WriteLine("SKIPPED: OPENAI_APIKEY not set — set env var to run this test");
            return;
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENAI_APIKEY"] = apiKey
            })
            .Build();

        using var service = new OpenAIRealtimeService(config);
        try
        {
            await service.ConnectAsync(Agents.Chess);
            var outcome = await WaitForLiveOutputAsync(
                h => service.TextReceived += h,
                h => service.TextReceived -= h,
                h => service.AudioReceived += h,
                h => service.AudioReceived -= h,
                h => service.ErrorOccurred += h,
                h => service.ErrorOccurred -= h,
                () => service.SendTextAsync("Reply with exactly OK."),
                TimeSpan.FromSeconds(20));

            _output.WriteLine($"OpenAI outcome: {outcome}");
            if (outcome.StartsWith("error:", StringComparison.OrdinalIgnoreCase) &&
                (outcome.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                 outcome.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase)))
            {
                _output.WriteLine("SKIPPED: OpenAI account has insufficient quota for realtime response");
                return;
            }

            (outcome.StartsWith("text:", StringComparison.OrdinalIgnoreCase) ||
             outcome.StartsWith("audio:", StringComparison.OrdinalIgnoreCase))
                .Should().BeTrue("OpenAI should emit either text or audio for a simple prompt");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"SKIPPED: OpenAI Realtime API unreachable — {ex.Message}");
            return;
        }
        catch (WebSocketException ex)
        {
            _output.WriteLine($"OpenAI Realtime WebSocket failure: {ex.Message}");
            throw;
        }
        finally
        {
            await service.DisconnectAsync();
        }
    }

    [Fact]
    public async Task OpenRouterClient_ChatCompletion_ReturnsResponse()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_APIKEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _output.WriteLine("SKIPPED: OPENROUTER_APIKEY not set — set env var to run this test");
            return;
        }

        using var http = new HttpClient
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        var client = new OpenRouterClient(http, apiKey, "openai/gpt-4o-mini");

        var request = new OpenRouterRequest
        {
            Model = "openai/gpt-4o-mini",
            Messages = new List<OpenRouterMessage>
            {
                new() { Role = "user", Content = "Reply with exactly: OK" }
            },
            MaxTokens = 10,
            Stream = false
        };

        OpenRouterResponse response;
        try
        {
            response = await client.ChatCompletionAsync(request);
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"SKIPPED: OpenRouter API unreachable — {ex.Message}");
            return;
        }
        catch (TaskCanceledException)
        {
            _output.WriteLine("SKIPPED: OpenRouter API timeout (15s)");
            return;
        }

        response.Should().NotBeNull();
        response.Choices.Should().NotBeEmpty();
        var content = response.Choices[0].Message.Content as string;
        _output.WriteLine($"OpenRouter response: '{content}'");
        content.Should().NotBeNullOrWhiteSpace();
        content!.Should().ContainEquivalentOf("OK", "simple 'reply with OK' prompt should yield OK");
    }

    private static async Task<string> WaitForLiveOutputAsync(
        Action<EventHandler<string>> subscribeText,
        Action<EventHandler<string>> unsubscribeText,
        Action<EventHandler<byte[]>> subscribeAudio,
        Action<EventHandler<byte[]>> unsubscribeAudio,
        Action<EventHandler<string>> subscribeError,
        Action<EventHandler<string>> unsubscribeError,
        Func<Task> sendAsync,
        TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnText(object? _, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                tcs.TrySetResult($"text:{text}");
            }
        }

        void OnAudio(object? _, byte[] audio)
        {
            if (audio.Length > 0)
            {
                tcs.TrySetResult($"audio:{audio.Length}");
            }
        }

        void OnError(object? _, string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                tcs.TrySetResult($"error:{error}");
            }
        }

        subscribeText(OnText);
        subscribeAudio(OnAudio);
        subscribeError(OnError);

        try
        {
            await sendAsync();
            using var cts = new CancellationTokenSource(timeout);
            using var registration = cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            return "error:timeout";
        }
        finally
        {
            unsubscribeText(OnText);
            unsubscribeAudio(OnAudio);
            unsubscribeError(OnError);
        }
    }

    private static bool TryBuildGeminiConfig(out IConfiguration config)
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_APIKEY")
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            config = null!;
            return false;
        }

        config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GEMINI_APIKEY"] = apiKey
            })
            .Build();
        return true;
    }
}
