using System.Text.Json;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class SessionTraceServiceTests : IDisposable
{
    private readonly string _traceDir;
    private readonly SessionTraceService _sut;

    public SessionTraceServiceTests()
    {
        _traceDir = Path.Combine(Path.GetTempPath(), $"gaimer-trace-test-{Guid.NewGuid():N}");
        _sut = new SessionTraceService(_traceDir);
    }

    public void Dispose()
    {
        _sut.Dispose();
        if (Directory.Exists(_traceDir))
            Directory.Delete(_traceDir, recursive: true);
    }

    // ── Interface conformance ─────────────────────────────────────────────

    [Fact]
    public void SessionTraceService_ImplementsISessionTraceService()
    {
        _sut.Should().BeAssignableTo<ISessionTraceService>();
    }

    // ── StartRun ──────────────────────────────────────────────────────────

    [Fact]
    public void StartRun_SetsRunId()
    {
        _sut.StartRun();

        _sut.RunId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void StartRun_CreatesTraceDirectory()
    {
        _sut.StartRun();

        Directory.Exists(_traceDir).Should().BeTrue();
    }

    [Fact]
    public void StartRun_CreatesTraceFile()
    {
        _sut.StartRun();

        var files = Directory.GetFiles(_traceDir, "*.jsonl");
        files.Should().HaveCount(1);
    }

    [Fact]
    public void StartRun_WritesBootstrapEvent()
    {
        _sut.StartRun();

        var events = ReadTraceEvents();
        events.Should().ContainSingle(e => e.EventName == "app.bootstrap");
    }

    // ── RunId / SessionId ─────────────────────────────────────────────────

    [Fact]
    public void RunId_IsNull_BeforeStartRun()
    {
        _sut.RunId.Should().BeNull();
    }

    [Fact]
    public void SessionId_IsNull_BeforeStartSession()
    {
        _sut.StartRun();
        _sut.SessionId.Should().BeNull();
    }

    // ── StartSession / EndSession ─────────────────────────────────────────

    [Fact]
    public void StartSession_SetsSessionId()
    {
        _sut.StartRun();
        _sut.StartSession();

        _sut.SessionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void StartSession_DoesNotEmitEvent()
    {
        _sut.StartRun();
        _sut.StartSession();

        var events = ReadTraceEvents();
        events.Should().NotContain(e => e.EventName == "session.connect.start",
            "StartSession only sets session ID — callers emit events with context");
    }

    [Fact]
    public void EndSession_WritesDisconnectEvent()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.EndSession();

        var events = ReadTraceEvents();
        events.Should().Contain(e => e.EventName == "session.disconnect");
    }

    [Fact]
    public void EndSession_ClearsSessionId()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.EndSession();

        _sut.SessionId.Should().BeNull();
    }

    [Fact]
    public void StartSession_GeneratesNewSessionId_EachTime()
    {
        _sut.StartRun();

        _sut.StartSession();
        var first = _sut.SessionId;
        _sut.EndSession();

        _sut.StartSession();
        var second = _sut.SessionId;

        first.Should().NotBe(second);
    }

    // ── TrackEvent ────────────────────────────────────────────────────────

    [Fact]
    public void TrackEvent_WritesStructuredEvent()
    {
        _sut.StartRun();
        _sut.TrackEvent("provider.selected", new Dictionary<string, string>
        {
            ["voice_provider"] = "openai",
            ["brain_provider"] = "openrouter"
        });

        var events = ReadTraceEvents();
        var providerEvent = events.FirstOrDefault(e => e.EventName == "provider.selected");
        providerEvent.Should().NotBeNull();
        providerEvent!.Payload.Should().ContainKey("voice_provider");
        providerEvent.Payload!["voice_provider"].Should().Be("openai");
    }

    [Fact]
    public void TrackEvent_IncludesRunId()
    {
        _sut.StartRun();
        _sut.TrackEvent("provider.selected");

        var events = ReadTraceEvents();
        events.Should().OnlyContain(e => e.RunId == _sut.RunId);
    }

    [Fact]
    public void TrackEvent_IncludesSessionId_WhenSessionActive()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.request.start");

        var events = ReadTraceEvents();
        var brainEvent = events.First(e => e.EventName == "brain.request.start");
        brainEvent.SessionId.Should().Be(_sut.SessionId);
    }

    [Fact]
    public void TrackEvent_HasNullSessionId_WhenNoSession()
    {
        _sut.StartRun();
        _sut.TrackEvent("provider.selected");

        var events = ReadTraceEvents();
        var providerEvent = events.First(e => e.EventName == "provider.selected");
        providerEvent.SessionId.Should().BeNull();
    }

    [Fact]
    public void TrackEvent_IncludesTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        _sut.StartRun();
        _sut.TrackEvent("provider.selected");
        var after = DateTimeOffset.UtcNow;

        var events = ReadTraceEvents();
        var providerEvent = events.First(e => e.EventName == "provider.selected");
        providerEvent.Timestamp.Should().BeOnOrAfter(before);
        providerEvent.Timestamp.Should().BeOnOrBefore(after);
    }

    // ── TrackError ────────────────────────────────────────────────────────

    [Fact]
    public void TrackError_WritesErrorEvent()
    {
        _sut.StartRun();
        _sut.TrackError("Brain connection failed", "brain");

        var events = ReadTraceEvents();
        var errorEvent = events.FirstOrDefault(e => e.EventName == "error");
        errorEvent.Should().NotBeNull();
        errorEvent!.Payload.Should().ContainKey("message");
        errorEvent.Payload!["message"].Should().Be("Brain connection failed");
        errorEvent.Payload.Should().ContainKey("source");
        errorEvent.Payload["source"].Should().Be("brain");
    }

    // ── TrackSessionResult ────────────────────────────────────────────────

    [Fact]
    public void TrackSessionResult_Success_WritesConnectSuccessEvent()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackSessionResult(success: true);

        var events = ReadTraceEvents();
        events.Should().Contain(e => e.EventName == "session.connect.success");
    }

    [Fact]
    public void TrackSessionResult_Failure_WritesConnectFailureEvent()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackSessionResult(success: false, error: "Timeout");

        var events = ReadTraceEvents();
        var failEvent = events.FirstOrDefault(e => e.EventName == "session.connect.failure");
        failEvent.Should().NotBeNull();
        failEvent!.Payload.Should().ContainKey("error");
    }

    // ── No secrets ────────────────────────────────────────────────────────

    [Fact]
    public void TrackEvent_DoesNotLogSecrets_InPayload()
    {
        _sut.StartRun();
        _sut.TrackEvent("provider.selected", new Dictionary<string, string>
        {
            ["voice_provider"] = "openai",
            ["has_api_key"] = "true"
        });

        var rawContent = ReadRawTrace();
        rawContent.Should().NotContain("sk-");
        rawContent.Should().NotContain("APIKEY");
    }

    // ── JSONL format ──────────────────────────────────────────────────────

    [Fact]
    public void TraceFile_IsValidJsonl()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("provider.selected");
        _sut.TrackError("test error", "test");
        _sut.EndSession();

        var lines = ReadRawTraceLines();
        lines.Should().HaveCountGreaterOrEqualTo(3); // bootstrap + provider + error + disconnect (StartSession does not emit)

        foreach (var line in lines)
        {
            var act = () => JsonDocument.Parse(line);
            act.Should().NotThrow($"each line should be valid JSON, but got: {line}");
        }
    }

    // ── EndRun ────────────────────────────────────────────────────────────

    [Fact]
    public void EndRun_WritesAppShutdownEvent()
    {
        _sut.StartRun();
        _sut.EndRun();

        var events = ReadTraceEvents();
        events.Should().Contain(e => e.EventName == "app.shutdown");
    }

    // ── Slice 2: Brain trace events ──────────────────────────────────

    [Fact]
    public void TrackEvent_BrainRequestStart_WritesStructuredEvent()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.request.start", new Dictionary<string, string>
        {
            ["correlation_id"] = "abc123",
            ["request_type"] = "image_analysis",
            ["model"] = "google/gemini-2.5-flash",
            ["image_bytes"] = "65536"
        });

        var events = ReadTraceEvents();
        var evt = events.FirstOrDefault(e => e.EventName == "brain.request.start");
        evt.Should().NotBeNull();
        evt!.Payload.Should().ContainKey("correlation_id");
        evt.Payload!["request_type"].Should().Be("image_analysis");
        evt.Payload["model"].Should().Be("google/gemini-2.5-flash");
        evt.SessionId.Should().Be(_sut.SessionId);
    }

    [Fact]
    public void TrackEvent_BrainRequestSuccess_WritesStructuredEvent()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.request.success", new Dictionary<string, string>
        {
            ["correlation_id"] = "abc123",
            ["request_type"] = "image_analysis",
            ["model"] = "google/gemini-2.5-flash",
            ["tool_turns"] = "2",
            ["response_length"] = "450"
        });

        var events = ReadTraceEvents();
        var evt = events.FirstOrDefault(e => e.EventName == "brain.request.success");
        evt.Should().NotBeNull();
        evt!.Payload!["tool_turns"].Should().Be("2");
        evt.Payload["response_length"].Should().Be("450");
    }

    [Fact]
    public void TrackEvent_BrainRequestFailure_WritesStructuredEvent()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.request.failure", new Dictionary<string, string>
        {
            ["correlation_id"] = "abc123",
            ["request_type"] = "query",
            ["error_type"] = "HttpRequestException"
        });

        var events = ReadTraceEvents();
        var evt = events.FirstOrDefault(e => e.EventName == "brain.request.failure");
        evt.Should().NotBeNull();
        evt!.Payload!["error_type"].Should().Be("HttpRequestException");
    }

    [Fact]
    public void BrainRequest_StartAndSuccess_LifecycleSymmetry()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.request.start", new Dictionary<string, string>
        {
            ["correlation_id"] = "sym001",
            ["request_type"] = "image_analysis",
            ["model"] = "test-model"
        });
        _sut.TrackEvent("brain.request.success", new Dictionary<string, string>
        {
            ["correlation_id"] = "sym001",
            ["request_type"] = "image_analysis",
            ["model"] = "test-model",
            ["tool_turns"] = "0",
            ["response_length"] = "100"
        });

        var events = ReadTraceEvents();
        var starts = events.Where(e => e.EventName == "brain.request.start").ToList();
        var successes = events.Where(e => e.EventName == "brain.request.success").ToList();
        starts.Should().HaveCount(1);
        successes.Should().HaveCount(1);
        starts[0].Payload!["correlation_id"].Should().Be(successes[0].Payload!["correlation_id"]);
    }

    [Fact]
    public void BrainRequest_StartAndFailure_LifecycleSymmetry()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.request.start", new Dictionary<string, string>
        {
            ["correlation_id"] = "sym002",
            ["request_type"] = "query",
            ["model"] = "test-model"
        });
        _sut.TrackEvent("brain.request.failure", new Dictionary<string, string>
        {
            ["correlation_id"] = "sym002",
            ["request_type"] = "query",
            ["error_type"] = "TimeoutException"
        });

        var events = ReadTraceEvents();
        var starts = events.Where(e => e.EventName == "brain.request.start").ToList();
        var failures = events.Where(e => e.EventName == "brain.request.failure").ToList();
        starts.Should().HaveCount(1);
        failures.Should().HaveCount(1);
        starts[0].Payload!["correlation_id"].Should().Be(failures[0].Payload!["correlation_id"]);
    }

    // ── Slice 2: Tool trace events ───────────────────────────────────

    [Fact]
    public void TrackEvent_BrainToolCall_WritesStructuredEvent()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.tool_call", new Dictionary<string, string>
        {
            ["tool_name"] = "analyze_position_engine"
        });

        var events = ReadTraceEvents();
        var evt = events.FirstOrDefault(e => e.EventName == "brain.tool_call");
        evt.Should().NotBeNull();
        evt!.Payload!["tool_name"].Should().Be("analyze_position_engine");
    }

    [Fact]
    public void TrackEvent_BrainToolResult_Success_WritesStructuredEvent()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.tool_result", new Dictionary<string, string>
        {
            ["tool_name"] = "get_game_state",
            ["duration_ms"] = "12",
            ["success"] = "true",
            ["result_length"] = "256"
        });

        var events = ReadTraceEvents();
        var evt = events.FirstOrDefault(e => e.EventName == "brain.tool_result");
        evt.Should().NotBeNull();
        evt!.Payload!["success"].Should().Be("true");
        evt.Payload["duration_ms"].Should().Be("12");
    }

    [Fact]
    public void TrackEvent_BrainToolResult_Failure_WritesStructuredEvent()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.tool_result", new Dictionary<string, string>
        {
            ["tool_name"] = "capture_screen",
            ["duration_ms"] = "5003",
            ["success"] = "false",
            ["error_type"] = "TimeoutException"
        });

        var events = ReadTraceEvents();
        var evt = events.FirstOrDefault(e => e.EventName == "brain.tool_result");
        evt.Should().NotBeNull();
        evt!.Payload!["success"].Should().Be("false");
        evt.Payload["error_type"].Should().Be("TimeoutException");
    }

    [Fact]
    public void ToolCall_AndResult_LifecycleSymmetry()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.tool_call", new Dictionary<string, string>
        {
            ["tool_name"] = "analyze_position_engine"
        });
        _sut.TrackEvent("brain.tool_result", new Dictionary<string, string>
        {
            ["tool_name"] = "analyze_position_engine",
            ["duration_ms"] = "800",
            ["success"] = "true",
            ["result_length"] = "512"
        });

        var events = ReadTraceEvents();
        var calls = events.Where(e => e.EventName == "brain.tool_call").ToList();
        var results = events.Where(e => e.EventName == "brain.tool_result").ToList();
        calls.Should().HaveCount(1);
        results.Should().HaveCount(1);
        calls[0].Payload!["tool_name"].Should().Be(results[0].Payload!["tool_name"]);
    }

    // ── Slice 2: Cancellation lifecycle symmetry ──────────────────────

    [Fact]
    public void BrainRequest_StartAndCancellation_LifecycleSymmetry()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.request.start", new Dictionary<string, string>
        {
            ["correlation_id"] = "cancel001",
            ["request_type"] = "image_analysis",
            ["model"] = "test-model"
        });
        _sut.TrackEvent("brain.request.failure", new Dictionary<string, string>
        {
            ["correlation_id"] = "cancel001",
            ["request_type"] = "image_analysis",
            ["error_type"] = "cancelled"
        });

        var events = ReadTraceEvents();
        var starts = events.Where(e => e.EventName == "brain.request.start").ToList();
        var terminals = events.Where(e => e.EventName == "brain.request.failure").ToList();
        starts.Should().HaveCount(1);
        terminals.Should().HaveCount(1);
        terminals[0].Payload!["error_type"].Should().Be("cancelled");
        starts[0].Payload!["correlation_id"].Should().Be(terminals[0].Payload["correlation_id"]);
    }

    [Fact]
    public void ToolCall_AndCancelledResult_LifecycleSymmetry()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.tool_call", new Dictionary<string, string>
        {
            ["tool_name"] = "analyze_position_engine"
        });
        _sut.TrackEvent("brain.tool_result", new Dictionary<string, string>
        {
            ["tool_name"] = "analyze_position_engine",
            ["duration_ms"] = "50",
            ["success"] = "false",
            ["error_type"] = "cancelled"
        });

        var events = ReadTraceEvents();
        var calls = events.Where(e => e.EventName == "brain.tool_call").ToList();
        var results = events.Where(e => e.EventName == "brain.tool_result").ToList();
        calls.Should().HaveCount(1);
        results.Should().HaveCount(1);
        results[0].Payload!["error_type"].Should().Be("cancelled");
        calls[0].Payload!["tool_name"].Should().Be(results[0].Payload["tool_name"]);
    }

    // ── Slice 2: Timeline trace events ───────────────────────────────

    [Fact]
    public void TrackEvent_TimelineEventEmitted_WritesStructuredEvent()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("timeline.event_emitted", new Dictionary<string, string>
        {
            ["output_type"] = "ImageAnalysis",
            ["summary_length"] = "42"
        });

        var events = ReadTraceEvents();
        var evt = events.FirstOrDefault(e => e.EventName == "timeline.event_emitted");
        evt.Should().NotBeNull();
        evt!.Payload!["output_type"].Should().Be("ImageAnalysis");
    }

    // ── Slice 2: No secrets in brain/tool payloads ───────────────────

    [Fact]
    public void BrainTraceEvents_DoNotContainSecrets()
    {
        _sut.StartRun();
        _sut.StartSession();
        _sut.TrackEvent("brain.request.start", new Dictionary<string, string>
        {
            ["correlation_id"] = "sec001",
            ["request_type"] = "image_analysis",
            ["model"] = "google/gemini-2.5-flash"
        });
        _sut.TrackEvent("brain.tool_call", new Dictionary<string, string>
        {
            ["tool_name"] = "analyze_position_engine"
        });

        var rawContent = ReadRawTrace();
        rawContent.Should().NotContain("sk-");
        rawContent.Should().NotContain("APIKEY");
        rawContent.Should().NotContain("Bearer");
    }

    // ── Thread safety ─────────────────────────────────────────────────────

    [Fact]
    public void TrackEvent_IsSafeUnderConcurrentCalls()
    {
        _sut.StartRun();

        var act = () =>
        {
            Parallel.For(0, 50, i =>
            {
                _sut.TrackEvent("test.concurrent", new Dictionary<string, string>
                {
                    ["iteration"] = i.ToString()
                });
            });
        };

        act.Should().NotThrow();

        var events = ReadTraceEvents();
        events.Where(e => e.EventName == "test.concurrent").Should().HaveCount(50);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private List<TraceEvent> ReadTraceEvents()
    {
        _sut.Flush();
        var lines = ReadRawTraceLines();
        return lines
            .Select(line => JsonSerializer.Deserialize<TraceEvent>(line, TraceEvent.JsonOptions)!)
            .ToList();
    }

    private string ReadRawTrace()
    {
        _sut.Flush();
        var file = Directory.GetFiles(_traceDir, "*.jsonl").FirstOrDefault();
        return file != null ? File.ReadAllText(file) : string.Empty;
    }

    private string[] ReadRawTraceLines()
    {
        _sut.Flush();
        var file = Directory.GetFiles(_traceDir, "*.jsonl").FirstOrDefault();
        if (file == null) return Array.Empty<string>();
        return File.ReadAllLines(file).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
    }
}
