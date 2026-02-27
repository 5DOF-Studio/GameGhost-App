using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.ViewModels;
using WitnessDesktop.Views;

namespace WitnessDesktop;

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

        // Unprefixed environment variables (for switches like USE_MOCK_SERVICES).
        // Note: .env files are NOT automatically loaded by .NET; they must be exported into the environment.
        configBuilder.AddEnvironmentVariables();
        
        return configBuilder.Build();
    }

    private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
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
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<ITimelineFeed, TimelineFeed>();
        services.AddSingleton<IChatPromptBuilder, ChatPromptBuilder>();
        services.AddSingleton<IBrainEventRouter>(sp =>
        {
            var timeline = sp.GetRequiredService<ITimelineFeed>();
            var provider = sp.GetService<IConversationProvider>();
            if (provider is null)
            {
                Console.WriteLine("[GameGhost][DI] WARNING: IConversationProvider is null — BrainEventRouter voice agent integration disabled.");
                System.Diagnostics.Debug.WriteLine("[GameGhost][DI] WARNING: IConversationProvider is null — BrainEventRouter voice agent disabled.");
            }
            return new BrainEventRouter(timeline, provider, null);
        });

        // Conversation provider - selected via factory based on environment variables.
        // See ConversationProviderFactory for selection logic (VOICE_PROVIDER, USE_MOCK_SERVICES, API keys).
        services.AddSingleton<ConversationProviderFactory>();
        services.AddSingleton<IConversationProvider>(sp =>
        {
            var factory = sp.GetRequiredService<ConversationProviderFactory>();
            return factory.Create();
        });
        
        // Legacy IGeminiService registration for backwards compatibility during transition.
        // TODO: Remove once all consumers migrate to IConversationProvider.
        var useMockServices = string.Equals(configuration["USE_MOCK_SERVICES"], "true", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(configuration["USE_MOCK_SERVICES"], "1", StringComparison.OrdinalIgnoreCase);

        var apiKey = configuration["GeminiApiKey"] ??
                     configuration["APIKEY"] ??   // from GEMINI_APIKEY
                     configuration["API_KEY"];    // from GEMINI_API_KEY
        
        if (!useMockServices && !string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine($"[GameGhost][DI] IGeminiService=GeminiLiveService (legacy, USE_MOCK_SERVICES={useMockServices}, apiKeyPresent=true)");
            System.Diagnostics.Debug.WriteLine($"[GameGhost][DI] IGeminiService=GeminiLiveService (legacy)");
            services.AddSingleton<IGeminiService, GeminiLiveService>();
        }
        else
        {
            Console.WriteLine($"[GameGhost][DI] IGeminiService=MockGeminiService (legacy, USE_MOCK_SERVICES={useMockServices}, apiKeyPresent={!string.IsNullOrEmpty(apiKey)})");
            System.Diagnostics.Debug.WriteLine($"[GameGhost][DI] IGeminiService=MockGeminiService (legacy)");
            services.AddSingleton<IGeminiService, MockGeminiService>();
        }
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<AgentSelectionViewModel>();
        services.AddSingleton<MainViewModel>();
        // Note: MinimalViewModel is no longer used - MinimalViewPage binds directly to MainViewModel
        // Kept for potential future use if separate minimal-view state is needed
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddTransient<AgentSelectionPage>();
        services.AddTransient<MainPage>();
        services.AddTransient<MinimalViewPage>();
#if DEBUG
        services.AddTransient<DevLauncherPage>();
        services.AddTransient<WorkbenchPage>();
#endif
    }
}
