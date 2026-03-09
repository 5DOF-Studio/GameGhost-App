using System.Text.Json;
using Xunit.Abstractions;
using GaimerDesktop.Models;
using GaimerDesktop.Services.Brain;

namespace GaimerDesktop.Tests.Integration;

[Trait("Category", "LiveApi")]
public class LiveApiTests
{
    private readonly ITestOutputHelper _output;

    // 1.e4 position — one of the most analyzed in Lichess cloud DB (depth 70+, ~300ms response)
    private const string KnownFen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";

    public LiveApiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task LichessCloudEval_KnownPosition_ReturnsEvalWithMoves()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var encodedFen = Uri.EscapeDataString(KnownFen);
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync($"https://lichess.org/api/cloud-eval?fen={encodedFen}");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"SKIPPED: No network — {ex.Message}");
            return;
        }
        catch (TaskCanceledException)
        {
            _output.WriteLine("SKIPPED: HTTP timeout — Lichess did not respond in 10s");
            return;
        }

        _output.WriteLine($"Lichess responded with {response.StatusCode}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Must return the queried FEN back
        root.GetProperty("fen").GetString().Should().Contain("4P3");

        // Must have analysis depth (this position is depth 70+ in Lichess cloud)
        var depth = root.GetProperty("depth").GetInt32();
        _output.WriteLine($"Cloud eval depth: {depth}");
        depth.Should().BeGreaterThan(20);

        // Must have at least one principal variation with moves and centipawn eval
        var pvs = root.GetProperty("pvs");
        pvs.GetArrayLength().Should().BeGreaterThan(0);

        var bestPv = pvs[0];
        var moves = bestPv.GetProperty("moves").GetString()!;
        moves.Should().NotBeNullOrWhiteSpace();

        // First move should be a valid UCI move (4-5 chars like "e7e5" or "g8f6")
        var bestMove = moves.Split(' ')[0];
        _output.WriteLine($"Best move: {bestMove}, moves: {moves}");
        bestMove.Length.Should().BeInRange(4, 5);

        // Should have centipawn evaluation (this position is roughly equal, +10 to +40 cp)
        bestPv.TryGetProperty("cp", out var cpProp).Should().BeTrue();
        var cp = cpProp.GetInt32();
        _output.WriteLine($"Centipawn eval: {cp}");
        cp.Should().BeInRange(-100, 200, "1.e4 position should be roughly equal");
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
}
