using Foundation;
using UIKit;
using WitnessDesktop.Services;

namespace WitnessDesktop;

[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        // Clear any saved state restoration activity to prevent position restoration
        session.StateRestorationActivity = null;

        CrashLogger.LogLifecycle("SceneDelegate", $"WillConnect — role={session.Role}");

        base.WillConnect(scene, session, connectionOptions);

        if (scene is UIWindowScene windowScene)
        {
            foreach (var window in windowScene.Windows)
                window.MakeKeyAndVisible();

            // Subscribe to scene lifecycle notifications for crash diagnostics
            var nc = NSNotificationCenter.DefaultCenter;
            nc.AddObserver(UIScene.DidDisconnectNotification, n =>
                CrashLogger.LogLifecycle("SceneDelegate", "DidDisconnect notification — scene session ended"), scene);
            nc.AddObserver(UIScene.DidEnterBackgroundNotification, n =>
                CrashLogger.LogLifecycle("SceneDelegate", "DidEnterBackground notification"), scene);
            nc.AddObserver(UIScene.WillEnterForegroundNotification, n =>
                CrashLogger.LogLifecycle("SceneDelegate", "WillEnterForeground notification"), scene);
            nc.AddObserver(UIScene.DidActivateNotification, n =>
                CrashLogger.LogLifecycle("SceneDelegate", "DidActivate notification"), scene);
            nc.AddObserver(UIScene.WillDeactivateNotification, n =>
                CrashLogger.LogLifecycle("SceneDelegate", "WillDeactivate notification"), scene);

            // UIApplication termination notification
            nc.AddObserver(UIApplication.WillTerminateNotification, n =>
                CrashLogger.LogLifecycle("SceneDelegate", "UIApplication WillTerminate notification"));
        }
    }

    /// <summary>
    /// Return null to prevent scene state (including window position) from being saved.
    /// Without this, Mac Catalyst restores the window to its previous position,
    /// which may be on a now-disconnected or secondary monitor.
    /// </summary>
    public override NSUserActivity? GetStateRestorationActivity(UIScene scene) => null;
}
