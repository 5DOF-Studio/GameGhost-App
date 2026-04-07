using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class GaimerPipeClientTests : IDisposable
{
    private readonly string _socketPath;
    private readonly GaimerPipeClient _sut;
    private Socket? _serverSocket;
    private Socket? _acceptedClient;

    public GaimerPipeClientTests()
    {
        _socketPath = Path.Combine(Path.GetTempPath(), $"gaimer-test-{Guid.NewGuid():N}.sock");
        _sut = new GaimerPipeClient(Mock.Of<ILogger<GaimerPipeClient>>());
    }

    public void Dispose()
    {
        _sut.Dispose();
        _acceptedClient?.Dispose();
        _serverSocket?.Dispose();
        if (File.Exists(_socketPath)) File.Delete(_socketPath);
    }

    private void StartServerSocket()
    {
        _serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _serverSocket.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _serverSocket.Listen(1);
    }

    private async Task<Socket> AcceptClientAsync()
    {
        _acceptedClient = await _serverSocket!.AcceptAsync();
        return _acceptedClient;
    }

    private static async Task<string> ReadLineFromSocket(Socket socket)
    {
        var stream = new NetworkStream(socket, ownsSocket: false);
        var reader = new StreamReader(stream, Encoding.UTF8);
        var line = await reader.ReadLineAsync();
        return line ?? "";
    }

    private static async Task WriteLineToSocket(Socket socket, string line)
    {
        var stream = new NetworkStream(socket, ownsSocket: false);
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        await writer.WriteLineAsync(line);
    }

    [Fact]
    public async Task ConnectAsync_ToListeningSocket_ReturnsTrue()
    {
        StartServerSocket();
        var connectTask = _sut.ConnectAsync(_socketPath);
        await AcceptClientAsync();
        var result = await connectTask;

        result.Should().BeTrue();
        _sut.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_ToNonexistentPath_ReturnsFalse()
    {
        var result = await _sut.ConnectAsync("/tmp/gaimer-nonexistent-socket.sock");
        result.Should().BeFalse();
        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WritesNdjsonLine()
    {
        StartServerSocket();
        var connectTask = _sut.ConnectAsync(_socketPath);
        var client = await AcceptClientAsync();
        await connectTask;

        await _sut.SendAsync("{\"type\":\"ping\"}");

        var received = await ReadLineFromSocket(client);
        received.Should().Be("{\"type\":\"ping\"}");
    }

    [Fact]
    public async Task MessageReceived_FiresForEachLine()
    {
        StartServerSocket();
        var connectTask = _sut.ConnectAsync(_socketPath);
        var client = await AcceptClientAsync();
        await connectTask;

        var messages = new List<string>();
        _sut.MessageReceived += (_, msg) => messages.Add(msg);

        await WriteLineToSocket(client, "{\"type\":\"pong\"}");
        await Task.Delay(200);

        messages.Should().ContainSingle().Which.Should().Be("{\"type\":\"pong\"}");
    }

    [Fact]
    public async Task ConnectionLost_FiresOnServerClose()
    {
        StartServerSocket();
        var connectTask = _sut.ConnectAsync(_socketPath);
        var client = await AcceptClientAsync();
        await connectTask;

        var lostFired = new TaskCompletionSource<bool>();
        _sut.ConnectionLost += (_, _) => lostFired.TrySetResult(true);

        client.Close();

        var result = await Task.WhenAny(lostFired.Task, Task.Delay(3000));
        result.Should().Be(lostFired.Task, "ConnectionLost should fire within 3s");
    }

    [Fact]
    public async Task SendAsync_WhenDisconnected_Throws()
    {
        var act = () => _sut.SendAsync("{\"test\":true}");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Disconnect_CleansUpAndSetsIsConnectedFalse()
    {
        StartServerSocket();
        var connectTask = _sut.ConnectAsync(_socketPath);
        await AcceptClientAsync();
        await connectTask;

        _sut.Disconnect();

        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentSendAsync_DoesNotInterleave()
    {
        StartServerSocket();
        var connectTask = _sut.ConnectAsync(_socketPath);
        var client = await AcceptClientAsync();
        await connectTask;

        // Send 10 messages concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(i => _sut.SendAsync($"{{\"n\":{i}}}"))
            .ToArray();
        await Task.WhenAll(tasks);

        // Read all lines — each should be a complete JSON line
        var stream = new NetworkStream(client, ownsSocket: false);
        var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = new List<string>();
        using var cts = new CancellationTokenSource(2000);
        while (lines.Count < 10)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (line == null) break;
            lines.Add(line);
        }

        lines.Should().HaveCount(10);
        lines.Should().AllSatisfy(line =>
            line.Should().MatchRegex(@"^\{""n"":\d+\}$"));
    }
}
