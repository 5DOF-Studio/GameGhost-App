using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

/// <summary>
/// Tests for ITelemetryService and ConsoleTelemetryService.
/// Validates structured telemetry output format, correlation IDs,
/// TrackEvent behavior, and TrackDuration disposable pattern.
/// </summary>
public class TelemetryServiceTests
{
    private readonly ConsoleTelemetryService _sut = new();

    // ── NewCorrelationId ──────────────────────────────────────────────────

    [Fact]
    public void NewCorrelationId_Returns8CharHexString()
    {
        var id = ConsoleTelemetryService.NewCorrelationId();

        id.Should().NotBeNullOrEmpty();
        id.Should().HaveLength(8);
    }

    [Fact]
    public void NewCorrelationId_ReturnsUniqueValuesOnSuccessiveCalls()
    {
        var id1 = ConsoleTelemetryService.NewCorrelationId();
        var id2 = ConsoleTelemetryService.NewCorrelationId();

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void NewCorrelationId_ContainsOnlyHexCharacters()
    {
        var id = ConsoleTelemetryService.NewCorrelationId();

        id.Should().MatchRegex("^[0-9a-f]{8}$");
    }

    // ── TrackEvent ────────────────────────────────────────────────────────

    [Fact]
    public void TrackEvent_DoesNotThrow_WithCategoryAndAction()
    {
        var act = () => _sut.TrackEvent("brain", "submit_image");

        act.Should().NotThrow();
    }

    [Fact]
    public void TrackEvent_DoesNotThrow_WithProperties()
    {
        var props = new Dictionary<string, string>
        {
            ["correlationId"] = "abc12345",
            ["bytes"] = "1024"
        };

        var act = () => _sut.TrackEvent("brain", "submit_image", props);

        act.Should().NotThrow();
    }

    [Fact]
    public void TrackEvent_DoesNotThrow_WithNullProperties()
    {
        var act = () => _sut.TrackEvent("brain", "submit_image", null);

        act.Should().NotThrow();
    }

    [Fact]
    public void TrackEvent_DoesNotThrow_WithEmptyProperties()
    {
        var act = () => _sut.TrackEvent("brain", "submit_image", new Dictionary<string, string>());

        act.Should().NotThrow();
    }

    // ── TrackDuration ─────────────────────────────────────────────────────

    [Fact]
    public void TrackDuration_ReturnsNonNullDisposable()
    {
        var disposable = _sut.TrackDuration("brain", "analysis");

        disposable.Should().NotBeNull();
        disposable.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public void TrackDuration_DisposableDoesNotThrowOnDispose()
    {
        var disposable = _sut.TrackDuration("brain", "analysis");

        var act = () => disposable.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void TrackDuration_WithProperties_ReturnsDisposable()
    {
        var props = new Dictionary<string, string>
        {
            ["correlationId"] = "abc12345"
        };

        var disposable = _sut.TrackDuration("brain", "analysis", props);

        disposable.Should().NotBeNull();
        disposable.Dispose(); // Should not throw
    }

    [Fact]
    public void TrackDuration_CanBeUsedInUsingStatement()
    {
        var act = () =>
        {
            using (_sut.TrackDuration("brain", "analysis"))
            {
                // Simulate some work
                Thread.Sleep(1);
            }
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void TrackDuration_MultipleDisposeCalls_DoNotThrow()
    {
        var disposable = _sut.TrackDuration("brain", "analysis");

        var act = () =>
        {
            disposable.Dispose();
            disposable.Dispose(); // Second dispose should be safe
        };

        act.Should().NotThrow();
    }

    // ── Interface conformance ─────────────────────────────────────────────

    [Fact]
    public void ConsoleTelemetryService_ImplementsITelemetryService()
    {
        _sut.Should().BeAssignableTo<ITelemetryService>();
    }

    // ── Output format verification ────────────────────────────────────────

    [Fact]
    public void TrackEvent_OutputContainsTelPrefix()
    {
        // Capture Debug output by using a TraceListener
        var listener = new StringTraceListener();
        System.Diagnostics.Trace.Listeners.Add(listener);
        try
        {
            _sut.TrackEvent("brain", "submit_image", new Dictionary<string, string>
            {
                ["bytes"] = "1024"
            });

            var output = listener.Output;
            output.Should().Contain("[TEL]");
            output.Should().Contain("brain.submit_image");
            output.Should().Contain("bytes=1024");
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public void TrackDuration_OutputContainsDurationMs()
    {
        var listener = new StringTraceListener();
        System.Diagnostics.Trace.Listeners.Add(listener);
        try
        {
            using (_sut.TrackDuration("brain", "analysis"))
            {
                Thread.Sleep(10);
            }

            var output = listener.Output;
            output.Should().Contain("[TEL]");
            output.Should().Contain("brain.analysis");
            output.Should().Contain("duration_ms=");
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    // ── Thread safety ─────────────────────────────────────────────────────

    [Fact]
    public void TrackEvent_IsSafeUnderConcurrentCalls()
    {
        var act = () =>
        {
            Parallel.For(0, 100, i =>
            {
                _sut.TrackEvent("test", "concurrent", new Dictionary<string, string>
                {
                    ["iteration"] = i.ToString()
                });
            });
        };

        act.Should().NotThrow();
    }

    /// <summary>
    /// Helper trace listener that captures Debug.WriteLine output.
    /// </summary>
    private class StringTraceListener : System.Diagnostics.TraceListener
    {
        private readonly System.Text.StringBuilder _sb = new();

        public string Output => _sb.ToString();

        public override void Write(string? message) => _sb.Append(message);
        public override void WriteLine(string? message) => _sb.AppendLine(message);
    }
}
