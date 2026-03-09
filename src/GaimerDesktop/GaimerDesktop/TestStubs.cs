// Stubs and global usings for the net8.0 library target only.
// These allow Models and Services that reference MAUI-only types
// to compile for plain net8.0 (used by the unit test project).
// Platform builds (maccatalyst, android, ios, windows) use the real MAUI types.
#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

// DI extension method (GetService<T>) for service locator patterns
global using Microsoft.Extensions.DependencyInjection;

// MAUI SDK provides these as global usings; net8.0 needs them explicitly
global using Microsoft.Maui.Graphics;
global using Microsoft.Maui.ApplicationModel;
global using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Controls
{
    public class ImageSource
    {
        public static ImageSource FromStream(Func<Stream> stream) => new();
        public void Cancel() { }
    }

    // Minimal Shell stub for MainViewModel (IQueryAttributable + Shell.Current navigation)
    public interface IQueryAttributable
    {
        void ApplyQueryAttributes(IDictionary<string, object> query);
    }

    public class Shell
    {
        public static Shell? Current { get; set; }
        public ShellNavigationState CurrentState { get; set; } = new();
        public INavigation Navigation { get; set; } = new StubNavigation();
        public Task GoToAsync(string route) => Task.CompletedTask;
        public Task GoToAsync(string route, IDictionary<string, object> parameters) => Task.CompletedTask;
        public Task DisplayAlert(string title, string message, string cancel) => Task.CompletedTask;
    }

    public interface INavigation
    {
        Task PushAsync(ContentPage page);
    }

    public class StubNavigation : INavigation
    {
        public Task PushAsync(ContentPage page) => Task.CompletedTask;
    }

    public class ContentPage { }


    public class ShellNavigationState
    {
        public Uri Location { get; set; } = new Uri("//MainPage", UriKind.Relative);
        public string OriginalString => Location.OriginalString;
    }

    // Minimal Application + Window stubs for ResizeWindowAsync
    public class Application
    {
        public static Application? Current { get; set; }
        public IList<Window> Windows { get; } = new List<Window>();
        public AppHandler? Handler { get; set; }
    }

    // Minimal handler/context stubs so service locator pattern compiles (always returns null in tests)
    public class AppHandler
    {
        public MauiContext? MauiContext { get; set; }
    }

    public class MauiContext
    {
        public IServiceProvider? Services { get; set; }
    }

    public class Window
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double MinimumWidth { get; set; }
        public double MinimumHeight { get; set; }
        public double MaximumWidth { get; set; }
        public double MaximumHeight { get; set; }
    }
}

namespace Microsoft.Maui.ApplicationModel
{
    public static class MainThread
    {
        public static bool IsMainThread => true;
        public static void BeginInvokeOnMainThread(Action action) => action();
        public static Task InvokeOnMainThreadAsync(Action action) { action(); return Task.CompletedTask; }
        public static Task InvokeOnMainThreadAsync(Func<Task> funcTask) => funcTask();
    }
}

// CommunityToolkit.Maui stubs for Toast (used in ShowChessInfoAsync)
namespace CommunityToolkit.Maui.Alerts
{
    public class Toast
    {
        public static Toast Make(string text, CommunityToolkit.Maui.Core.ToastDuration duration, double fontSize)
            => new();
        public Task Show(CancellationToken ct = default) => Task.CompletedTask;
    }
}

namespace CommunityToolkit.Maui.Core
{
    public enum ToastDuration { Short, Long }
}

// Dispatching namespace stub (used by CommunityToolkit.Mvvm source generators)
namespace Microsoft.Maui.Dispatching
{
    public interface IDispatcher
    {
        bool Dispatch(Action action);
    }
}

// MainPage stub in the root GaimerDesktop namespace (matches MainPage.xaml.cs)
namespace GaimerDesktop
{
    public class MainPage : Microsoft.Maui.Controls.ContentPage
    {
        public string? AgentId { get; set; }
    }
}

#endif
