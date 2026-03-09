using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GaimerDesktop.Services.Chess;

/// <summary>
/// Manages a Stockfish child process, communicating via UCI protocol over stdin/stdout.
/// Thread-safe: only one analysis can run at a time (serialized via SemaphoreSlim).
/// </summary>
public sealed class StockfishService : IStockfishService
{
    private readonly StockfishDownloader _downloader;
    private readonly ILogger<StockfishService> _logger;
    private readonly SemaphoreSlim _analysisLock = new(1, 1);

    private Process? _process;
    private StreamWriter? _stdin;
    private Task? _stdoutReaderTask;
    private CancellationTokenSource? _processCts;

    // Collected output lines from stdout reader loop
    private readonly List<string> _outputLines = new();
    private readonly SemaphoreSlim _outputSignal = new(0);
    private volatile bool _disposed;

    public bool IsReady { get; private set; }
    public bool IsInstalled => StockfishDownloader.IsInstalled();

    public StockfishService(StockfishDownloader downloader, ILogger<StockfishService> logger)
    {
        _downloader = downloader;
        _logger = logger;
    }

    public async Task<bool> EnsureInstalledAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (IsInstalled) return true;

        var assetName = StockfishDownloader.GetAssetName();
        var url = $"https://github.com/official-stockfish/Stockfish/releases/latest/download/{assetName}";

        return await _downloader.DownloadAsync(url, progress: progress, ct: ct);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_process is not null && !_process.HasExited)
        {
            _logger.LogDebug("[Stockfish] Already running");
            return;
        }

        var enginePath = StockfishDownloader.GetEnginePath();
        if (!File.Exists(enginePath))
            throw new FileNotFoundException("Stockfish binary not found. Call EnsureInstalledAsync first.", enginePath);

        _processCts = new CancellationTokenSource();

        var psi = new ProcessStartInfo
        {
            FileName = enginePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = psi };
        _process.Start();

        _stdin = _process.StandardInput;
        _stdin.AutoFlush = true;

        // Read stderr asynchronously (logging only — NEVER block on this)
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _logger.LogDebug("[Stockfish stderr] {Line}", e.Data);
        };
        _process.BeginErrorReadLine();

        // Start stdout reader loop
        _stdoutReaderTask = Task.Run(() => ReadStdoutLoop(_processCts.Token), _processCts.Token);

        // UCI handshake
        await SendCommandAsync("uci");
        await WaitForLineAsync("uciok", TimeSpan.FromSeconds(10), ct);

        // Configure engine
        await SendCommandAsync("setoption name Threads value 2");
        await SendCommandAsync("setoption name Hash value 128");

        await SendCommandAsync("isready");
        await WaitForLineAsync("readyok", TimeSpan.FromSeconds(10), ct);

        IsReady = true;
        _logger.LogInformation("[Stockfish] Engine ready (PID={Pid})", _process.Id);
    }

    public async Task StopAsync()
    {
        IsReady = false;

        if (_stdin is not null)
        {
            try
            {
                await _stdin.WriteLineAsync("quit");
                await _stdin.FlushAsync();
            }
            catch { /* process may already be gone */ }
        }

        if (_process is not null && !_process.HasExited)
        {
            try
            {
                if (!_process.WaitForExit(3000))
                    _process.Kill(entireProcessTree: true);
            }
            catch { /* best effort */ }
        }

        _processCts?.Cancel();

        if (_stdoutReaderTask is not null)
        {
            try { await _stdoutReaderTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        CleanupProcess();
    }

    public async Task<EngineAnalysis> AnalyzePositionAsync(
        string fen,
        AnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        if (!IsReady)
            throw new InvalidOperationException("Engine is not ready. Call StartAsync first.");

        if (!FenValidator.IsValid(fen, out var errors))
            throw new ArgumentException($"Invalid FEN: {string.Join("; ", errors)}", nameof(fen));

        options ??= new AnalysisOptions();

        await _analysisLock.WaitAsync(ct);
        try
        {
            // Clear any leftover output
            ClearOutput();

            // Set position
            await SendCommandAsync($"position fen {fen}");

            // Configure MultiPV if > 1
            if (options.MultiPv > 1)
                await SendCommandAsync($"setoption name MultiPV value {options.MultiPv}");

            // Start analysis
            await SendCommandAsync($"go movetime {options.MoveTimeMs} depth {options.MaxDepth}");

            // Collect output until bestmove
            var variations = new Dictionary<int, EngineVariation>();
            long nodes = 0;
            int timeMs = 0;
            string? bestMove = null;
            string? ponderMove = null;

            while (!ct.IsCancellationRequested)
            {
                var line = await WaitForAnyLineAsync(TimeSpan.FromSeconds(options.MoveTimeMs / 1000.0 + 10), ct);
                if (line is null) break;

                // Parse info lines — keep latest for each MultiPV index
                var variation = UciParser.ParseInfoLine(line);
                if (variation is not null)
                {
                    variations[variation.MultiPvIndex] = variation;
                    var lineNodes = UciParser.ParseNodes(line);
                    if (lineNodes > 0) nodes = lineNodes;
                    var lineTime = UciParser.ParseTime(line);
                    if (lineTime > 0) timeMs = lineTime;
                }

                // Parse bestmove
                var bm = UciParser.ParseBestMove(line);
                if (bm is not null)
                {
                    bestMove = bm.Value.BestMove;
                    ponderMove = bm.Value.PonderMove;
                    break;
                }
            }

            // Reset MultiPV to 1 if we changed it
            if (options.MultiPv > 1)
                await SendCommandAsync("setoption name MultiPV value 1");

            if (bestMove is null)
                throw new TimeoutException("Stockfish did not return bestmove within timeout");

            var maxDepth = variations.Count > 0 ? variations.Values.Max(v => v.Depth) : 0;
            var sortedVariations = variations.Values
                .OrderBy(v => v.MultiPvIndex)
                .ToList()
                .AsReadOnly();

            return new EngineAnalysis(
                BestMove: bestMove,
                PonderMove: ponderMove,
                Depth: maxDepth,
                Variations: sortedVariations,
                Nodes: nodes,
                TimeMs: timeMs);
        }
        finally
        {
            _analysisLock.Release();
        }
    }

    // ── Stdout Reader Loop ────────────────────────────────────────────────

    private async Task ReadStdoutLoop(CancellationToken ct)
    {
        var reader = _process!.StandardOutput;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break; // EOF — process exited

                lock (_outputLines)
                {
                    _outputLines.Add(line);
                }
                _outputSignal.Release();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Stockfish] Stdout reader exited with error");
        }
    }

    // ── Command Helpers ───────────────────────────────────────────────────

    private async Task SendCommandAsync(string command)
    {
        if (_stdin is null) throw new InvalidOperationException("Process not started");
        _logger.LogDebug("[Stockfish ->] {Command}", command);
        await _stdin.WriteLineAsync(command);
    }

    private async Task WaitForLineAsync(string expected, TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            await _outputSignal.WaitAsync(timeoutCts.Token);

            lock (_outputLines)
            {
                for (int i = _outputLines.Count - 1; i >= 0; i--)
                {
                    if (_outputLines[i].StartsWith(expected))
                    {
                        _outputLines.Clear();
                        return;
                    }
                }
            }
        }
    }

    private async Task<string?> WaitForAnyLineAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await _outputSignal.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        lock (_outputLines)
        {
            if (_outputLines.Count > 0)
            {
                var line = _outputLines[0];
                _outputLines.RemoveAt(0);
                return line;
            }
        }
        return null;
    }

    private void ClearOutput()
    {
        lock (_outputLines)
        {
            _outputLines.Clear();
        }
        // Drain signal semaphore
        while (_outputSignal.CurrentCount > 0)
            _outputSignal.Wait(0);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────

    private void CleanupProcess()
    {
        _stdin?.Dispose();
        _stdin = null;
        _process?.Dispose();
        _process = null;
        _processCts?.Dispose();
        _processCts = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        IsReady = false;
        _processCts?.Cancel();

        if (_process is not null && !_process.HasExited)
        {
            try
            {
                _stdin?.WriteLine("quit");
                if (!_process.WaitForExit(2000))
                    _process.Kill(entireProcessTree: true);
            }
            catch { /* best effort */ }
        }

        CleanupProcess();
        _analysisLock.Dispose();
        _outputSignal.Dispose();
    }
}
