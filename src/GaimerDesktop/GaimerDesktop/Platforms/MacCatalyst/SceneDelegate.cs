using Foundation;
using UIKit;

namespace GaimerDesktop;

[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        // Clear any saved state restoration activity to prevent position restoration
        session.StateRestorationActivity = null;

        base.WillConnect(scene, session, connectionOptions);

        if (scene is UIWindowScene windowScene)
        {
            foreach (var window in windowScene.Windows)
                window.MakeKeyAndVisible();
        }
    }

    /// <summary>
    /// Return null to prevent scene state (including window position) from being saved.
    /// Without this, Mac Catalyst restores the window to its previous position,
    /// which may be on a now-disconnected or secondary monitor.
    /// </summary>
    public override NSUserActivity? GetStateRestorationActivity(UIScene scene) => null;
}
