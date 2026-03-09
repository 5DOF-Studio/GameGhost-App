using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Auth;
using GaimerDesktop.Services.Brain;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.Services.Conversation;
using GaimerDesktop.ViewModels;
using GaimerDesktop.Views;

namespace GaimerDesktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
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

        // Environment variables with GEMINI_ prefix
        // GEMINI_APIKEY -> configuration["APIKEY"]
        configBuilder.AddEnvironmentVariables("GEMINI_");
        
        // Environment variables with OPENAI_ prefix
        // OPENAI_APIKEY -> configuration["OPENAI_APIKEY"] (also via unprefixed load)
        configBuilder.AddEnvironmentVariables("OPENAI_");

        // Environment variables with OPENROUTER_ prefix
        // OPENROUTER_APIKEY -> configuration["APIKEY"] (shares key name with Gemini prefix)
        // To avoid collision, also check under the full env var name via unprefixed load
        configBuilder.AddEnvironmentVariables("OPENROUTER_");

        // Unprefixed environment variables (for switches like USE_MOCK_SERVICES).
        // Note: .env files are NOT automatically loaded by .NET; they must be exported into the environment.
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
        // Settings + Auth — registered first, other services may depend on them
        services.AddSingleton<ISettingsService, SettingsService>();

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
        services.AddSingleton<IBrainContextService, BrainContextService>();
        services.AddSingleton<IFrameDiffService, FrameDiffService>();

        // Stockfish chess engine
        services.AddSingleton(sp => new StockfishDownloader(
            new HttpClient(),
            sp.GetService<ILogger<StockfishDownloader>>()));
        if (useMockServices)
            services.AddSingleton<IStockfishService, MockStockfishService>();
        else
            services.AddSingleton<IStockfishService, StockfishService>();

        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<ITimelineFeed, TimelineFeed>();
        services.AddSingleton<IChatPromptBuilder, ChatPromptBuilder>();
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
            return new BrainEventRouter(timeline, provider, null, brainContext);
        });

        // Conversation provider - selected via factory based on environment variables.
        // See ConversationProviderFactory for selection logic (VOICE_PROVIDER, USE_MOCK_SERVICES, API keys).
        services.AddSingleton<ConversationProviderFactory>(sp =>
            new ConversationProviderFactory(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ISettingsService>()));
        services.AddSingleton<IConversationProvider>(sp =>
        {
            var factory = sp.GetRequiredService<ConversationProviderFactory>();
            return factory.Create();
        });
        
        // Legacy IGeminiService registration for backwards compatibility during transition.
        // TODO: Remove once all consumers migrate to IConversationProvider.
        var apiKey = configuration["GeminiApiKey"] ??
                     configuration["APIKEY"] ??   // from GEMINI_APIKEY
                     configuration["API_KEY"];    // from GEMINI_API_KEY
        
        if (!useMockServices && !string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine($"[Gaimer][DI] IGeminiService=GeminiLiveService (legacy, USE_MOCK_SERVICES={useMockServices}, apiKeyPresent=true)");
            System.Diagnostics.Debug.WriteLine($"[Gaimer][DI] IGeminiService=GeminiLiveService (legacy)");
            services.AddSingleton<IGeminiService, GeminiLiveService>();
        }
        else
        {
            Console.WriteLine($"[Gaimer][DI] IGeminiService=MockGeminiService (legacy, USE_MOCK_SERVICES={useMockServices}, apiKeyPresent={!string.IsNullOrEmpty(apiKey)})");
            System.Diagnostics.Debug.WriteLine($"[Gaimer][DI] IGeminiService=MockGeminiService (legacy)");
            services.AddSingleton<IGeminiService, MockGeminiService>();
        }

        // Brain service — REST LLM pipeline via OpenRouter (or mock when no API key)
        // Read directly from environment to avoid collision with GEMINI_ prefix stripping (both strip to "APIKEY")
        var openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_APIKEY");

        if (!useMockServices && !string.IsNullOrEmpty(openRouterKey))
        {
            Console.WriteLine($"[Gaimer][DI] IBrainService=OpenRouterBrainService (apiKeyPresent=true)");
            services.AddSingleton<IBrainService>(sp =>
            {
                var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
                var httpClient = new HttpClient(handler);
                var client = new OpenRouterClient(httpClient, openRouterKey, "anthropic/claude-sonnet-4");

                var lichessClient = new HttpClient { BaseAddress = new Uri("https://lichess.org/") };
                lichessClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                lichessClient.Timeout = TimeSpan.FromSeconds(5);

                var toolExecutor = new ToolExecutor(
                    sp.GetRequiredService<IWindowCaptureService>(),
                    sp.GetRequiredService<ISessionManager>(),
                    client,
                    lichessClient,
                    sp.GetRequiredService<IStockfishService>(),
                    "openai/gpt-4o-mini",
                    sp.GetRequiredService<ILogger<ToolExecutor>>());
                return new OpenRouterBrainService(client, toolExecutor, sp.GetRequiredService<ISessionManager>());
            });
        }
        else
        {
            Console.WriteLine($"[Gaimer][DI] IBrainService=MockBrainService (USE_MOCK_SERVICES={useMockServices}, openRouterKeyPresent={!string.IsNullOrEmpty(openRouterKey)})");
            services.AddSingleton<IBrainService, MockBrainService>();
        }
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
        services.AddTransient<SettingsViewModel>();
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
}
