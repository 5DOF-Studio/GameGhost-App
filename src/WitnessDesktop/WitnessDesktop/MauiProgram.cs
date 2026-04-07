using System.Text;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Auth;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.Services.History;
using WitnessDesktop.Services.Local;
using WitnessDesktop.Services.Replay;
using WitnessDesktop.ViewModels;
using WitnessDesktop.Views;

namespace WitnessDesktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Tee Console.Out to /tmp/gaimer-debug.log so diagnostics are visible
        // even when the app is launched via `open` (GUI env, no terminal stdout).
        SetupFileLogging();

        var builder = MauiApp.CreateBuilder();
        
        // Build configuration from multiple sources
        var configuration = BuildConfiguration();
        builder.Services.AddSingleton<IConfiguration>(configuration);
        
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Orbitron-Bold.ttf", "OrbitronBold");
                fonts.AddFont("Orbitron-Regular.ttf", "OrbitronRegular");
                fonts.AddFont("Rajdhani-Regular.ttf", "RajdhaniRegular");
                fonts.AddFont("Rajdhani-SemiBold.ttf", "RajdhaniSemiBold");
                fonts.AddFont("Rajdhani-Bold.ttf", "RajdhaniBold");
                fonts.AddFont("Krophed.otf", "Krophed");
            });

        RegisterServices(builder.Services, configuration);
        RegisterViewModels(builder.Services);
        RegisterViews(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
    
    private static IConfiguration BuildConfiguration()
    {
        // Load .env file into process environment (dev convenience — no NuGet dependency)
        LoadDotEnv();

        var configBuilder = new ConfigurationBuilder();

        // User secrets (development) - optional, won't fail if not configured
        try
        {
            configBuilder.AddUserSecrets<App>(optional: true, reloadOnChange: false);
        }
        catch
        {
            // User secrets not available on this platform - that's OK
        }

        // Unprefixed environment variables (for switches and provider keys).
        // Provider keys are read under their explicit names to avoid prefix-strip
        // collisions like GEMINI_APIKEY/OPENROUTER_APIKEY -> "APIKEY".
        configBuilder.AddEnvironmentVariables();
        
        return configBuilder.Build();
    }

    /// <summary>
    /// Loads key=value pairs from a .env file into the process environment.
    /// Searches up from the app base directory to find the project root .env.
    /// Skips silently if no .env file is found.
    /// </summary>
    private static void LoadDotEnv()
    {
        // Search common locations for .env file
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gaimer", ".env"),
        };

        // Also walk up from base directory (works when running from repo via dotnet run)
        var dir = AppContext.BaseDirectory;
        var walkCandidates = new List<string>(candidates);
        for (var i = 0; i < 10 && dir != null; i++)
        {
            walkCandidates.Add(Path.Combine(dir, ".env"));
            dir = Directory.GetParent(dir)?.FullName;
        }

        var envPath = walkCandidates.FirstOrDefault(File.Exists);
        if (envPath is null) return;

        System.Diagnostics.Debug.WriteLine($"[Gaimer] Loading .env from {envPath}");
        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex <= 0) continue;

            var key = trimmed[..eqIndex].Trim();
            var value = trimmed[(eqIndex + 1)..].Trim();

            // Only set if not already defined (real env vars take precedence)
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Telemetry — registered first, other services may depend on it
        services.AddSingleton<ITelemetryService, ConsoleTelemetryService>();

        // Session trace — structured JSONL event ledger for post-run debugging
        services.AddSingleton<ISessionTraceService>(sp =>
        {
            var traceDir = Path.Combine(
                FileSystem.AppDataDirectory, "traces");
            var service = new SessionTraceService(traceDir);
            service.StartRun();
            return service;
        });

        // History + Replay — shared database path for session persistence and retrieval
        var historyDbPath = Path.Combine(FileSystem.AppDataDirectory, "phase07", "gaimer-history.db");

        services.AddSingleton<ISessionHistoryService>(sp =>
            new SessionHistoryService(historyDbPath, sp.GetService<ISessionTraceService>()));

        // Settings + Auth — registered first, other services may depend on them
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IStructuralSettingsTracker, StructuralSettingsTracker>();

        var useMockServices = string.Equals(configuration["USE_MOCK_SERVICES"], "true", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(configuration["USE_MOCK_SERVICES"], "1", StringComparison.OrdinalIgnoreCase);

        if (useMockServices)
            services.AddSingleton<IAuthService, MockAuthService>();
        else
            services.AddSingleton<IAuthService, SupabaseAuthService>();

#if WINDOWS
        services.AddSingleton<IAudioService, Platforms.Windows.AudioService>();
#elif MACCATALYST
        // MacCatalyst uses split recording/playback engines to avoid format conflicts (24kHz playback vs 48kHz mic native).
        services.AddSingleton<Services.Audio.IAudioRecordingService, Platforms.MacCatalyst.RecordingService>();
        services.AddSingleton<Services.Audio.IAudioPlaybackService, Platforms.MacCatalyst.PlaybackService>();
        services.AddSingleton<IAudioService, Services.Audio.CompositeAudioService>();
#else
        services.AddSingleton<IAudioService, MockAudioService>();
#endif
#if MACCATALYST
        services.AddSingleton<IWindowCaptureService, Platforms.MacCatalyst.WindowCaptureService>();
#elif WINDOWS
        services.AddSingleton<IWindowCaptureService, Platforms.Windows.WindowCaptureService>();
#else
        services.AddSingleton<IWindowCaptureService, MockWindowCaptureService>();
#endif

#if MACCATALYST
        services.AddSingleton<IGhostModeService, Platforms.MacCatalyst.MacGhostModeService>();
#else
        services.AddSingleton<IGhostModeService, MockGhostModeService>();
#endif
        services.AddSingleton<IVisualReelService, VisualReelService>();
        services.AddSingleton<IObservationStore>(_ =>
            new SqliteObservationStore(Path.Combine(FileSystem.AppDataDirectory, "observations")));
        services.AddSingleton<IObservationAdmissionGate, ObservationAdmissionGate>();

        // Replay services — retrieval, anchor, and media presentation
        services.AddSingleton<IReplayRetrievalService>(sp =>
            new ReplayRetrievalService(historyDbPath, sp.GetRequiredService<IObservationStore>()));
        services.AddSingleton<IReplayAnchorService>(sp =>
            new ReplayAnchorService(historyDbPath, sp.GetRequiredService<IReplayRetrievalService>()));
        services.AddSingleton<IReplayMediaPresentationService, ReplayMediaPresentationService>();

            // Replay recording service (continuous screen recording with segment rotation)
#if MACCATALYST
            services.AddSingleton<INativeRecordingBridge, Platforms.MacCatalyst.NativeRecordingBridge>();
#endif
            services.AddSingleton<IReplayRecordingService>(sp =>
            {
                var replayDir = Path.Combine(FileSystem.AppDataDirectory, "replays");
#if MACCATALYST
                var bridge = sp.GetRequiredService<INativeRecordingBridge>();
#else
                INativeRecordingBridge? bridge = null;
#endif
                var trace = sp.GetService<ISessionTraceService>();
                return bridge != null
                    ? new ReplayRecordingService(bridge, replayDir, trace)
                    : new NullReplayRecordingService();
            });

        // --- Phase 2: Video Analysis ---
        var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GEMINI_APIKEY");
        var geminiFlashModel = Environment.GetEnvironmentVariable("GEMINI_FLASH_MODEL") ?? "gemini-2.5-flash";
        var geminiProModel = Environment.GetEnvironmentVariable("GEMINI_PRO_MODEL") ?? "gemini-3-pro-preview";

        services.AddSingleton<ISegmentAnalysisStore>(sp =>
            new SqliteSegmentAnalysisStore(historyDbPath, sp.GetService<ISessionTraceService>()));

        if (!string.IsNullOrEmpty(geminiApiKey))
        {
            services.AddSingleton<GeminiVideoClient>(sp =>
            {
                var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
                var httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
                    Timeout = TimeSpan.FromMinutes(3)
                };
                return new GeminiVideoClient(httpClient, geminiApiKey,
                    sessionTrace: sp.GetService<ISessionTraceService>());
            });

            services.AddSingleton<IVideoAnalysisTool>(sp =>
                new VideoAnalysisTool(
                    sp.GetRequiredService<GeminiVideoClient>(),
                    geminiFlashModel,
                    geminiProModel,
                    sp.GetService<ISessionTraceService>()));

            services.AddSingleton<IReplayAnalysisOrchestrator>(sp =>
                new ReplayAnalysisOrchestrator(
                    sp.GetRequiredService<IVideoAnalysisTool>(),
                    sp.GetRequiredService<ISegmentAnalysisStore>(),
                    sp.GetRequiredService<IGameSkillPackService>(),
                    sp.GetService<ISessionTraceService>(),
                    sp.GetRequiredService<GeminiVideoClient>()));
        }

        services.AddSingleton<IBrainContextService>(sp =>
            new BrainContextService(
                sp.GetRequiredService<IVisualReelService>(),
                sp.GetService<ITelemetryService>(),
                sp.GetService<IExchangeManager>()));
        services.AddSingleton<IVoiceTranscriptStore, VoiceTranscriptStore>();
        services.AddSingleton<IFrameDiffService, FrameDiffService>();

        // Stockfish chess engine
        services.AddSingleton(sp => new StockfishDownloader(
            new HttpClient(),
            sp.GetService<ILogger<StockfishDownloader>>()));
        if (useMockServices)
            services.AddSingleton<IStockfishService, MockStockfishService>();
        else
            services.AddSingleton<IStockfishService, StockfishService>();

        // MAUI bundles GamePacks into Contents/Resources/ (not Contents/MonoBundle/)
        // AppContext.BaseDirectory points to MonoBundle on Mac Catalyst
        var gamePacksDir = Path.Combine(AppContext.BaseDirectory, "GamePacks");
        if (!Directory.Exists(gamePacksDir))
            gamePacksDir = Path.Combine(AppContext.BaseDirectory, "..", "Resources", "GamePacks");
        services.AddSingleton<IGameSkillPackService>(sp =>
            new GameSkillPackService(gamePacksDir, sp.GetService<ISessionTraceService>()));
        services.AddSingleton<IBrainPromptBuilder, BrainPromptBuilder>();
        services.AddSingleton<IGameJournalService>(sp =>
            new GameJournalService(sp.GetService<ITelemetryService>()));
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<ITimelineFeed>(sp =>
            new TimelineFeed(
                sp.GetRequiredService<ISessionManager>(),
                sp.GetService<ISessionTraceService>(),
                sp.GetService<ISessionHistoryService>()));
        services.AddSingleton<IChatPromptBuilder, ChatPromptBuilder>();
        services.AddSingleton<IVoiceGroundingCoordinator>(sp =>
            new VoiceGroundingCoordinator(packService: sp.GetService<IGameSkillPackService>()));

        // Exchange state machine (Phase 12A) + degradation + telemetry (12E)
        services.AddSingleton<IExchangeManager>(sp =>
            new ExchangeManager(
                packService: sp.GetService<IGameSkillPackService>(),
                telemetry: sp.GetService<ITelemetryService>()));
        services.AddSingleton<Services.Audio.IWakePhraseDetector, Services.Audio.TranscriptWakePhraseDetector>();

        // Porcupine wake word detection (primary — D-AI-7)
        // Falls back gracefully (IsAvailable=false) when no access key is set.
        var picovoiceKey = Environment.GetEnvironmentVariable("PICOVOICE_ACCESS_KEY");
        services.AddSingleton<Services.Audio.IPorcupineWakeDetector>(
            new Services.Audio.PorcupineWakeDetector(picovoiceKey));
        services.AddSingleton<IVoiceDeliveryGate>(sp =>
            new VoiceDeliveryGate(
                sp.GetRequiredService<IExchangeManager>(),
                sp.GetService<IBargeInPolicyService>(),
                sp.GetService<Services.Audio.IUserSpeechDetector>(),
                sp.GetService<Services.Audio.IAgentSpeechTracker>(),
                sp.GetService<ISessionTraceService>()));

        // Audio speech tracking (Phase 12B)
        services.AddSingleton<Services.Audio.IAgentSpeechTracker, Services.Audio.AgentSpeechTracker>();
        services.AddSingleton<Services.Audio.IUserSpeechDetector, Services.Audio.UserSpeechDetector>();

        // SFX one-shot player (Phase 12C)
        services.AddSingleton<Services.Audio.ISfxPlayer, Services.Audio.SfxPlayer>();

        // Barge-in policy + reminder queue (Phase 12C)
        services.AddSingleton<IBargeInPolicyService, BargeInPolicyService>();
        services.AddSingleton<IReminderQueue, ReminderQueue>();

        // Brain request channel (Phase 12D) — voice-to-brain deferral priority queue
        services.AddSingleton<IBrainRequestChannel, BrainRequestChannel>();

        // Gaimer Team multi-agent service (Phase B)
        services.AddSingleton<IGaimerPipeClient, GaimerPipeClient>();
        services.AddSingleton<IClaudeProcessManager, ClaudeProcessManager>();
        if (useMockServices)
            services.AddSingleton<IGaimerTeamService, MockGaimerTeamService>();
        else
            services.AddSingleton<IGaimerTeamService, GaimerTeamService>();

        services.AddSingleton<IBrainEventRouter>(sp =>
        {
            var timeline = sp.GetRequiredService<ITimelineFeed>();
            var provider = sp.GetService<IConversationProvider>();
            var brainContext = sp.GetService<IBrainContextService>();
            if (provider is null)
            {
                Console.WriteLine("[Gaimer][DI] WARNING: IConversationProvider is null — BrainEventRouter voice agent integration disabled.");
                System.Diagnostics.Debug.WriteLine("[Gaimer][DI] WARNING: IConversationProvider is null — BrainEventRouter voice agent disabled.");
            }
            var gameJournal = sp.GetService<IGameJournalService>();
            var frameDiff = sp.GetService<IFrameDiffService>();
            var telemetry = sp.GetService<ITelemetryService>();
            var voiceGrounding = sp.GetService<IVoiceGroundingCoordinator>();
            var voiceTranscriptStore = sp.GetService<IVoiceTranscriptStore>();
            var historyService = sp.GetService<ISessionHistoryService>();
            var sessionTrace = sp.GetService<ISessionTraceService>();
            var reminderQueue = sp.GetService<IReminderQueue>();
            return new BrainEventRouter(timeline, provider, null, brainContext,
                telemetry: telemetry,
                gameJournal: gameJournal,
                frameDiffService: frameDiff,
                onNewGameDetected: summary =>
                {
                    System.Diagnostics.Debug.WriteLine($"[DI] New game detected — previous summary: {summary?.Length ?? 0} chars");
                },
                voiceGrounding: voiceGrounding,
                voiceTranscriptStore: voiceTranscriptStore,
                historyService: historyService,
                sessionTrace: sessionTrace,
                packService: sp.GetService<IGameSkillPackService>(),
                voiceDeliveryGate: sp.GetService<IVoiceDeliveryGate>(),
                reminderQueue: reminderQueue,
                exchangeManager: sp.GetService<IExchangeManager>());
        });

        // Conversation provider - selected via factory based on environment variables.
        // See ConversationProviderFactory for selection logic (VOICE_PROVIDER, USE_MOCK_SERVICES, API keys).
        services.AddSingleton<ConversationProviderFactory>(sp =>
            new ConversationProviderFactory(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetService<ILocalAudioConversationClient>()));
        services.AddSingleton<IConversationProvider>(sp =>
        {
            var factory = sp.GetRequiredService<ConversationProviderFactory>();
            return factory.Create();
        });


        // Brain service — resolved via BrainServiceFactory (local-first, cloud, or mock)
        var openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_APIKEY");
        var cloudBrainAvailable = !useMockServices && !string.IsNullOrEmpty(openRouterKey);

        var ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://127.0.0.1:11434";
        var localBrainModel = Environment.GetEnvironmentVariable("GAIMER_LOCAL_BRAIN_MODEL") ??
                              Environment.GetEnvironmentVariable("OLLAMA_MODEL") ??
                              "minicpm-v";
        var localInferenceTimeoutMinutes =
            int.TryParse(Environment.GetEnvironmentVariable("GAIMER_LOCAL_TIMEOUT_MINUTES"), out var timeoutMinutes) &&
            timeoutMinutes > 0
                ? timeoutMinutes
                : 10;

        // Local speech providers — platform-specific STT/TTS
#if MACCATALYST
        services.AddSingleton<Platforms.MacCatalyst.IGaimerSpeechInterop, Platforms.MacCatalyst.GaimerSpeechInterop>();
        services.AddSingleton<ISpeechToTextProvider>(sp =>
            new Platforms.MacCatalyst.MacSpeechToTextProvider(
                sp.GetRequiredService<Platforms.MacCatalyst.IGaimerSpeechInterop>()));
        services.AddSingleton<ITextToSpeechProvider>(sp =>
            new Platforms.MacCatalyst.MacTextToSpeechProvider(
                sp.GetRequiredService<Platforms.MacCatalyst.IGaimerSpeechInterop>()));
#else
        services.AddSingleton<ISpeechToTextProvider, StubSpeechToTextProvider>();
        services.AddSingleton<ITextToSpeechProvider, StubTextToSpeechProvider>();
#endif

        services.AddSingleton<ILocalTextConversationBackend>(_ =>
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(ollamaBaseUrl),
                Timeout = TimeSpan.FromMinutes(localInferenceTimeoutMinutes)
            };
            return new OllamaTextConversationBackend(client, localBrainModel);
        });
        services.AddSingleton<ILocalAudioConversationClient>(sp =>
            new LocalVoiceConversationClient(
                sp.GetRequiredService<ILocalTextConversationBackend>(),
                sp.GetRequiredService<ISpeechToTextProvider>(),
                sp.GetRequiredService<ITextToSpeechProvider>()));
        services.AddSingleton<ILocalModelRuntime>(sp =>
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(ollamaBaseUrl),
                Timeout = TimeSpan.FromSeconds(2)
            };
            return new OllamaLocalModelRuntime(
                client, localBrainModel, voiceEnabled: true,
                stt: sp.GetRequiredService<ISpeechToTextProvider>(),
                tts: sp.GetRequiredService<ITextToSpeechProvider>());
        });
        services.AddSingleton<ILocalVisionInferenceClient>(_ =>
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(ollamaBaseUrl),
                Timeout = TimeSpan.FromMinutes(localInferenceTimeoutMinutes)
            };
            return new OllamaLocalVisionInferenceClient(client, localBrainModel);
        });
        services.AddSingleton<InferenceProviderPolicy>();
        services.AddSingleton<BrainServiceFactory>();

        services.AddSingleton<IBrainService>(sp =>
        {
            var factory = sp.GetRequiredService<BrainServiceFactory>();
            BrainFactoryResult result;
            try
            {
                // Keep DI synchronous, but isolate the async policy/preflight path from the UI thread.
                result = Task.Run(() => factory.CreateAsync(
                    createCloudBrain: () =>
                    {
#if DEBUG
                        // Demo mode: use pre-baked Shoothouse CoD demo instead of cloud brain
                        Console.WriteLine("[Gaimer][DI] Creating DEMO brain service (Shoothouse)...");
                        return new DemoBrainService_Shoothouse(
                            sp.GetRequiredService<ILogger<DemoBrainService_Shoothouse>>());
#else
                        Console.WriteLine("[Gaimer][DI] Creating cloud brain service...");
                        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
                        var httpClient = new HttpClient(handler);
                        var client = new OpenRouterClient(httpClient, openRouterKey!, "google/gemini-2.5-flash",
                            sp.GetService<IGameSkillPackService>());

                        var toolExecutor = new ToolExecutor(
                            sp.GetRequiredService<IWindowCaptureService>(),
                            sp.GetRequiredService<ISessionManager>(),
                            client,
                            sp.GetRequiredService<IStockfishService>(),
                            "openai/gpt-4o-mini",
                            sp.GetRequiredService<ILogger<ToolExecutor>>(),
                            telemetry: sp.GetService<ITelemetryService>(),
                            gameJournal: sp.GetService<IGameJournalService>(),
                            sessionTrace: sp.GetService<ISessionTraceService>(),
                            segmentAnalysisStore: sp.GetService<ISegmentAnalysisStore>(),
                            videoAnalysisTool: sp.GetService<IVideoAnalysisTool>(),
                            replayRecording: sp.GetService<IReplayRecordingService>(),
                            packService: sp.GetService<IGameSkillPackService>(),
                            gaimerTeam: sp.GetService<IGaimerTeamService>());
                        return new OpenRouterBrainService(
                            client, toolExecutor, sp.GetRequiredService<ISessionManager>(),
                            brainPromptBuilder: sp.GetRequiredService<IBrainPromptBuilder>(),
                            // IMPORTANT: OpenRouterBrainService overrides the client default
                            // model on every image-analysis request. Pass the production
                            // vision model explicitly here so DI cannot drift to the service
                            // constructor default.
                            brainModel: "google/gemini-2.5-flash",
                            telemetry: sp.GetService<ITelemetryService>(),
                            gameJournal: sp.GetService<IGameJournalService>(),
                            brainContext: sp.GetService<IBrainContextService>(),
                            sessionTrace: sp.GetService<ISessionTraceService>(),
                            voiceTranscriptStore: sp.GetService<IVoiceTranscriptStore>());
#endif
                    },
                    createLocalBrain: () =>
                    {
                        Console.WriteLine("[Gaimer][DI] Creating local brain service...");
                        var localClient = sp.GetRequiredService<ILocalVisionInferenceClient>();
                        var sessionManager = sp.GetRequiredService<ISessionManager>();
                        var brainPromptBuilder = sp.GetRequiredService<IBrainPromptBuilder>();
                        var telemetry = sp.GetService<ITelemetryService>();
                        var gameJournal = sp.GetService<IGameJournalService>();
                        var brainContext = sp.GetService<IBrainContextService>();

                        return new LocalMiniCpmBrainService(
                            localClient,
                            sessionManager,
                            brainPromptBuilder: brainPromptBuilder,
                            telemetry: telemetry,
                            gameJournal: gameJournal,
                            brainContext: brainContext);
                    },
                    createMockBrain: () =>
                    {
                        Console.WriteLine("[Gaimer][DI] Creating mock brain service...");
                        return new MockBrainService(sp.GetRequiredService<ILogger<MockBrainService>>());
                    },
                    cloudBrainAvailable: cloudBrainAvailable
                )).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gaimer][DI] IBrainService resolution failed: {ex}");
                throw;
            }

            Console.WriteLine($"[Gaimer][DI] IBrainService={result.Brain.ProviderName} (mode={result.Selection.Mode}, reason={result.Reason})");

            sp.GetService<ISessionTraceService>()?.TrackEvent("provider.selected", new Dictionary<string, string>
            {
                ["brain_provider"] = result.Brain.ProviderName,
                ["inference_mode"] = result.Selection.Mode.ToString(),
                ["reason"] = result.Reason ?? "none"
            });

            return result.Brain;
        });
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<AgentSelectionViewModel>(sp =>
            new AgentSelectionViewModel(sp.GetRequiredService<IStockfishService>()));
        services.AddTransient<OnboardingViewModel>(sp =>
            new OnboardingViewModel(
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<IStockfishService>(),
                sp.GetService<ISettingsService>()));
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>(sp => new SettingsViewModel(
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<ILocalModelRuntime>()));
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddTransient<AgentSelectionPage>();
        services.AddTransient<OnboardingPage>();
        services.AddTransient<MainPage>();
        services.AddTransient<MinimalViewPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<UnauthorizedPage>();
        services.AddTransient<ErrorPage>();
#if DEBUG
        services.AddTransient<DevLauncherPage>();
        services.AddTransient<WorkbenchPage>();
#endif
    }

    private static void SetupFileLogging()
    {
        try
        {
            var logPath = "/tmp/gaimer-debug.log";
            var fileStream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var fileWriter = new StreamWriter(fileStream) { AutoFlush = true };
            var tee = new TeeTextWriter(Console.Out, fileWriter);
            Console.SetOut(tee);
            Console.SetError(new TeeTextWriter(Console.Error, fileWriter));
            // Write directly to file as a canary — proves file was created even if Console.SetOut fails
            fileWriter.WriteLine($"[Gaimer] Log file: {logPath} (started {DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            fileWriter.WriteLine($"[Gaimer] BaseDirectory: {AppContext.BaseDirectory}");
        }
        catch (Exception ex)
        {
            // Last resort — try writing the failure reason
            try { File.WriteAllText("/tmp/gaimer-debug-error.txt", ex.ToString()); } catch { }
        }
    }
}

/// <summary>
/// TextWriter that writes to two underlying writers simultaneously (tee pattern).
/// Used to mirror Console.Out to a log file for GUI-launched Mac Catalyst apps
/// where stdout is not visible.
/// </summary>
internal sealed class TeeTextWriter : TextWriter
{
    private readonly TextWriter _primary;
    private readonly TextWriter _secondary;

    public TeeTextWriter(TextWriter primary, TextWriter secondary)
    {
        _primary = primary;
        _secondary = secondary;
    }

    public override Encoding Encoding => _primary.Encoding;

    public override void Write(char value)
    {
        _primary.Write(value);
        _secondary.Write(value);
    }

    public override void Write(string? value)
    {
        _primary.Write(value);
        _secondary.Write(value);
    }

    public override void WriteLine(string? value)
    {
        _primary.WriteLine(value);
        _secondary.WriteLine(value);
    }

    public override void Flush()
    {
        _primary.Flush();
        _secondary.Flush();
    }
}
