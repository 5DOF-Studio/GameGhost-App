using Foundation;
using UIKit;

namespace GaimerDesktop;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
	{
		Console.WriteLine("[AppDelegate] FinishedLaunching called");
		try
		{
			var result = base.FinishedLaunching(application, launchOptions);
			Console.WriteLine($"[AppDelegate] FinishedLaunching returned {result}");
#pragma warning disable CA1422
			Console.WriteLine($"[AppDelegate] Windows count: {application.Windows.Length}");
#pragma warning restore CA1422
			Console.WriteLine($"[AppDelegate] ConnectedScenes count: {application.ConnectedScenes.Count}");
			foreach (var scene in application.ConnectedScenes)
			{
				Console.WriteLine($"[AppDelegate] Scene: {scene.GetType().Name}, state={scene.ActivationState}");
				if (scene is UIWindowScene ws)
					Console.WriteLine($"[AppDelegate] WindowScene windows: {ws.Windows.Length}");
			}
			return result;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[AppDelegate] FinishedLaunching EXCEPTION: {ex}");
			throw;
		}
	}

	// Prevent state restoration — corrupted state causes SIGSEGV in ViewDidLoad
	// during scene transition when restoring from prior session.
	[Export("application:shouldRestoreSecureApplicationState:")]
	public bool ShouldRestoreSecureApplicationState(UIApplication application, NSCoder coder) => false;

	[Export("application:shouldSaveSecureApplicationState:")]
	public bool ShouldSaveSecureApplicationState(UIApplication application, NSCoder coder) => false;

	public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession connectingSceneSession, UIKit.UISceneConnectionOptions options)
	{
		Console.WriteLine($"[AppDelegate] GetConfiguration called, role={connectingSceneSession.Role}");

		// .NET 8 MAUI bug on Mac Catalyst: base.GetConfiguration returns DelegateType=null
		// and an empty config name when UIApplicationSceneManifest is in Info.plist.
		// Create the config entirely from scratch with the correct MAUI scene delegate.
		var config = new UISceneConfiguration(
			"__MAUI_DEFAULT_SCENE_CONFIGURATION__",
			connectingSceneSession.Role);
		config.DelegateType = typeof(SceneDelegate);

		Console.WriteLine($"[AppDelegate] GetConfiguration returning: name={config.Name}, delegateType={config.DelegateType?.Name ?? "null"}");
		return config;
	}
}
