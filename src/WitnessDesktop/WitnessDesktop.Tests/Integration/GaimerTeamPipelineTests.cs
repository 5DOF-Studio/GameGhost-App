using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Integration;

public class GaimerTeamPipelineTests
{
    [Fact]
    public void DI_ResolvesGaimerPipeClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGaimerPipeClient, GaimerPipeClient>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IGaimerPipeClient>();
        client.Should().BeOfType<GaimerPipeClient>();
    }

    [Fact]
    public void DI_ResolvesClaudeProcessManager()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService>(Mock.Of<ISettingsService>());
        services.AddSingleton<IClaudeProcessManager, ClaudeProcessManager>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var manager = sp.GetRequiredService<IClaudeProcessManager>();
        manager.Should().BeOfType<ClaudeProcessManager>();
    }

    [Fact]
    public void DI_ResolvesGaimerTeamService_WithDependencies()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGaimerPipeClient, GaimerPipeClient>();
        services.AddSingleton<IClaudeProcessManager, ClaudeProcessManager>();
        services.AddSingleton<ISettingsService>(Mock.Of<ISettingsService>());
        services.AddSingleton<IGaimerTeamService, GaimerTeamService>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var team = sp.GetRequiredService<IGaimerTeamService>();
        team.Should().BeOfType<GaimerTeamService>();
    }

    [Fact]
    public void DI_MockMode_ResolvesMockGaimerTeamService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGaimerTeamService, MockGaimerTeamService>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var team = sp.GetRequiredService<IGaimerTeamService>();
        team.Should().BeOfType<MockGaimerTeamService>();
    }

    [Fact]
    public async Task Roundtrip_PipeClientToMockServer()
    {
        var socketPath = Path.Combine(Path.GetTempPath(), $"gaimer-rt-{Guid.NewGuid():N}.sock");
        try
        {
            // Start a mock server
            using var serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            serverSocket.Bind(new UnixDomainSocketEndPoint(socketPath));
            serverSocket.Listen(1);

            // Connect pipe client
            using var client = new GaimerPipeClient(Mock.Of<ILogger<GaimerPipeClient>>());
            var connectTask = client.ConnectAsync(socketPath);
            using var accepted = await serverSocket.AcceptAsync();
            var connected = await connectTask;
            connected.Should().BeTrue();

            // Set up message capture
            var messages = new List<string>();
            client.MessageReceived += (_, msg) => messages.Add(msg);

            // Client sends task_request
            var taskReq = JsonSerializer.Serialize(new
            {
                type = "task_request",
                id = "gt_roundtrip01",
                task = "Test roundtrip"
            });
            await client.SendAsync(taskReq);

            // Server reads it
            var serverStream = new NetworkStream(accepted, ownsSocket: false);
            var reader = new StreamReader(serverStream, Encoding.UTF8);
            var receivedLine = await reader.ReadLineAsync();
            receivedLine.Should().NotBeNull();

            var parsed = JsonDocument.Parse(receivedLine!);
            parsed.RootElement.GetProperty("type").GetString().Should().Be("task_request");
            parsed.RootElement.GetProperty("id").GetString().Should().Be("gt_roundtrip01");

            // Server sends back task_result
            var writer = new StreamWriter(serverStream, Encoding.UTF8) { AutoFlush = true };
            var resultJson = JsonSerializer.Serialize(new
            {
                type = "task_result",
                task_id = "gt_roundtrip01",
                status = "complete",
                response = "Roundtrip works!"
            });
            await writer.WriteLineAsync(resultJson);
            await Task.Delay(200);

            // Client receives it
            messages.Should().ContainSingle();
            var resultParsed = JsonDocument.Parse(messages[0]);
            resultParsed.RootElement.GetProperty("type").GetString().Should().Be("task_result");
            resultParsed.RootElement.GetProperty("response").GetString().Should().Be("Roundtrip works!");
        }
        finally
        {
            if (File.Exists(socketPath)) File.Delete(socketPath);
        }
    }

    [Fact]
    public void ChannelPlugin_ClaudeMdExists()
    {
        // Try well-known dev path
        var claudeMdPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Developer", "5DOF-Studio", "Gaimer-app", "src", "gaimer-channel-plugin", "CLAUDE.md");

        if (!File.Exists(claudeMdPath))
            return; // Skip in CI — no source tree

        var content = File.ReadAllText(claudeMdPath);
        content.Should().Contain("submit_result");
        content.Should().Contain("send_status");
        content.Should().Contain("Safety Boundaries");
    }
}
