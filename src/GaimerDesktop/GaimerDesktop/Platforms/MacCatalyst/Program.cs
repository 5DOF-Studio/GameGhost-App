using ObjCRuntime;
using UIKit;

namespace GaimerDesktop;

public class Program
{
	static void Main(string[] args)
	{
		// Catch unhandled managed exceptions before they become ObjC SIGSEGV
		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			Console.WriteLine($"[UNHANDLED] {e.ExceptionObject}");
		};

		ObjCRuntime.Runtime.MarshalManagedException += (s, e) =>
		{
			Console.WriteLine($"[MARSHAL_MANAGED] {e.Exception}");
		};

		// Delete Mac Catalyst scene session saved state BEFORE UIApplication.Main.
		// This prevents window position restoration from a previous session
		// (which may reference a disconnected external monitor).
		ClearSceneSavedState();

		UIApplication.Main(args, null, typeof(AppDelegate));
	}

	private static void ClearSceneSavedState()
	{
		try
		{
			// Mac Catalyst stores scene sessions in:
			// ~/Library/Saved Application State/{bundleId}~iosmac.savedState/
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var savedStateDir = Path.Combine(home, "Library", "Saved Application State");

			if (!Directory.Exists(savedStateDir)) return;

			foreach (var dir in Directory.GetDirectories(savedStateDir, "*gaimer*iosmac*"))
			{
				Console.WriteLine($"[Program] Deleting scene saved state: {dir}");
				Directory.Delete(dir, recursive: true);
			}

			// Also clear any NSWindow Frame keys from NSUserDefaults
			var defaults = Foundation.NSUserDefaults.StandardUserDefaults;
			var dict = defaults.ToDictionary();
			foreach (var key in dict.Keys)
			{
				var keyStr = key.ToString();
				if (keyStr != null && keyStr.Contains("NSWindow Frame"))
				{
					Console.WriteLine($"[Program] Removing NSWindow Frame key: {keyStr}");
					defaults.RemoveObject(keyStr);
				}
			}
			defaults.Synchronize();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[Program] ClearSceneSavedState error: {ex.Message}");
		}
	}
}
