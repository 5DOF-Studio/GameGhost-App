using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace WitnessDesktop.Services;

public sealed class GaimerPipeClient : IGaimerPipeClient
{
    private readonly ILogger<GaimerPipeClient> _logger;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private Socket? _socket;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _readCts;
    private Task? _readLoop;
    private bool _disposed;
    private const int MaxLineLength = 1_048_576; // 1MB (PB-M1)

    public GaimerPipeClient(ILogger<GaimerPipeClient> logger)
    {
        _logger = logger;
    }

    private volatile bool _isConnected; // PB-M3
    public bool IsConnected => _isConnected;

    public event EventHandler<string>? MessageReceived;
    public event EventHandler? ConnectionLost;

    public async Task<bool> ConnectAsync(string socketPath, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await _socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

            _stream = new NetworkStream(_socket, ownsSocket: true);
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = false };

            _readCts = new CancellationTokenSource();
            _readLoop = Task.Run(() => ReadLoopAsync(_readCts.Token), CancellationToken.None);

            _isConnected = true;
            _logger.LogInformation("[GaimerPipe] Connected to {Path}", socketPath);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or FileNotFoundException)
        {
            _logger.LogWarning("[GaimerPipe] Failed to connect to {Path}: {Message}", socketPath, ex.Message);
            CleanupSocket();
            return false;
        }
    }

    public async Task SendAsync(string jsonLine, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected || _writer == null)
            throw new InvalidOperationException("Pipe is not connected.");

        await _writeSemaphore.WaitAsync(ct);
        try
        {
            await _writer.WriteLineAsync(jsonLine.AsMemory(), ct);
            await _writer.FlushAsync(ct);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public void Disconnect()
    {
        if (!IsConnected) return;
        _logger.LogInformation("[GaimerPipe] Disconnecting");
        _readCts?.Cancel();
        if (_readLoop != null)
        {
            try { _readLoop.Wait(TimeSpan.FromSeconds(2)); } catch { /* read loop should exit quickly after CTS cancel */ }
            _readLoop = null;
        }
        CleanupSocket();
        _isConnected = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _writeSemaphore.Dispose();
        _readCts?.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await _reader!.ReadLineAsync(ct);
                if (line == null)
                {
                    _logger.LogInformation("[GaimerPipe] Server closed connection");
                    _isConnected = false;
                    ConnectionLost?.Invoke(this, EventArgs.Empty);
                    return;
                }
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (line.Length > MaxLineLength)
                {
                    _logger.LogWarning("[GaimerPipe] Dropping oversized message ({Length} bytes)", line.Length);
                    continue;
                }
                MessageReceived?.Invoke(this, line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Disconnect()
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            if (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("[GaimerPipe] Read loop error: {Message}", ex.Message);
                _isConnected = false;
                ConnectionLost?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void CleanupSocket()
    {
        try { _reader?.Dispose(); } catch { /* best-effort */ }
        try { _writer?.Dispose(); } catch { /* best-effort */ }
        try { _stream?.Dispose(); } catch { /* best-effort */ }
        try { _socket?.Dispose(); } catch { /* best-effort */ }
        _reader = null;
        _writer = null;
        _stream = null;
        _socket = null;
    }
}
