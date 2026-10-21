using Microsoft.Web.WebView2.Core;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

internal static class DesktopTestWindowCleanup
{
    public static async Task CloseAndDeleteProfileAsync(
        MainWindow? window,
        string profile)
    {
        CoreWebView2? core = window?.PreviewWebView.CoreWebView2;
        CoreWebView2Environment? environment = core?.Environment;
        uint? browserProcessId = core?.BrowserProcessId;
        TaskCompletionSource<bool> exited = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnBrowserExited(object? sender, CoreWebView2BrowserProcessExitedEventArgs args)
        {
            if (args.BrowserProcessId == browserProcessId)
            {
                exited.TrySetResult(true);
            }
        }

        if (environment is not null)
        {
            environment.BrowserProcessExited += OnBrowserExited;
        }
        try
        {
            if (window is not null)
            {
                ((MainViewModel)window.DataContext).MarkSaved(string.Empty);
                window.Close();
            }
            if (environment is not null)
            {
                // Dispose closes the controller synchronously, but the Runtime
                // releases the profile asynchronously. This event attests that
                // all associated processes and user-data-folder resources exited.
                try
                {
                    await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (TimeoutException exception)
                {
                    throw new TimeoutException(
                        $"WebView2 browser {browserProcessId} did not release isolated profile '{profile}' after Window.Close.",
                        exception);
                }
            }

            if (Directory.Exists(profile))
            {
                Directory.Delete(profile, recursive: true);
            }
        }
        finally
        {
            if (environment is not null)
            {
                environment.BrowserProcessExited -= OnBrowserExited;
            }
        }
    }
}
