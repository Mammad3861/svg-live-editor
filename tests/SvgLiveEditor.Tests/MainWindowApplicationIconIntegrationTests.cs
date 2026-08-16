using System.Windows.Interop;
using System.Windows.Threading;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MainWindowApplicationIconIntegrationTests
{
    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public async Task RealWindow_ExposesCustomSmallAndLargeIcons()
    {
        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(
                    Dispatcher.CurrentDispatcher));
            _ = RunHarnessAsync(completion);
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Task finished = await Task.WhenAny(
            completion.Task,
            Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.AreSame(
            completion.Task,
            finished,
            "The isolated icon STA did not complete within 30 seconds.");
        Assert.IsTrue(
            thread.Join(TimeSpan.FromSeconds(5)),
            $"The isolated icon STA did not exit; state={thread.ThreadState}.");
        Assert.IsTrue(await completion.Task);
    }

    private static async Task RunHarnessAsync(
        TaskCompletionSource<bool> completion)
    {
        string localApplicationData = Path.Combine(
            Path.GetTempPath(),
            "SvgLiveEditor.Icon.Tests",
            Guid.NewGuid().ToString("N"));
        MainWindow? window = null;
        TaskCompletionSource<bool>? webViewInitialization = null;
        Exception? failure = null;
        try
        {
            window = new MainWindow(localApplicationData)
            {
                Width = 900,
                Height = 600,
                ShowActivated = false
            };
            webViewInitialization = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            window.PreviewWebView.CoreWebView2InitializationCompleted +=
                (_, _) => webViewInitialization.TrySetResult(true);
            window.Show();
            await Dispatcher.Yield(DispatcherPriority.Render);

            nint hwnd = new WindowInteropHelper(window).Handle;
            Assert.AreNotEqual(nint.Zero, hwnd);
            nint small = WindowsIconTestSupport.GetWindowIcon(
                hwnd,
                WindowsIconTestSupport.IconSmall);
            nint small2 = WindowsIconTestSupport.GetWindowIcon(
                hwnd,
                WindowsIconTestSupport.IconSmall2);
            nint big = WindowsIconTestSupport.GetWindowIcon(
                hwnd,
                WindowsIconTestSupport.IconBig);
            Assert.AreNotEqual(nint.Zero, small);
            Assert.AreNotEqual(nint.Zero, small2);
            Assert.AreNotEqual(nint.Zero, big);

            Assert.IsTrue(
                WindowsIconTestSupport.HandleContainsSvgLiveEditorBrandPalette(
                    small),
                "ICON_SMALL did not contain the custom application artwork.");
            Assert.IsTrue(
                WindowsIconTestSupport.HandleContainsSvgLiveEditorBrandPalette(
                    small2),
                "ICON_SMALL2 did not contain the custom application artwork.");
            Assert.IsTrue(
                WindowsIconTestSupport.HandleContainsSvgLiveEditorBrandPalette(
                    big),
                "ICON_BIG did not contain the custom application artwork.");

            AssertClassIconIsUnsetOrCustom(
                hwnd,
                WindowsIconTestSupport.ClassSmallIcon);
            AssertClassIconIsUnsetOrCustom(
                hwnd,
                WindowsIconTestSupport.ClassBigIcon);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            try
            {
                if (window is not null)
                {
                    if (webViewInitialization is not null)
                    {
                        try
                        {
                            await webViewInitialization.Task.WaitAsync(
                                TimeSpan.FromSeconds(20));
                            await Dispatcher.Yield(
                                DispatcherPriority.ApplicationIdle);
                        }
                        catch (TimeoutException)
                        {
                        }
                    }
                    GetViewModel(window).MarkSaved(string.Empty);
                    window.Close();
                }
            }
            catch (Exception exception)
            {
                failure = CombineFailures(failure, exception);
            }

            try
            {
                await Task.Delay(250);
                await DeleteDirectoryWithRetriesAsync(localApplicationData);
            }
            catch (Exception exception)
            {
                failure = CombineFailures(failure, exception);
            }

            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(
                DispatcherPriority.Background);
            if (failure is null)
            {
                completion.TrySetResult(true);
            }
            else
            {
                completion.TrySetException(failure);
            }
        }
    }

    private static void AssertClassIconIsUnsetOrCustom(
        nint hwnd,
        int index)
    {
        nint classIcon = WindowsIconTestSupport.GetClassIcon(hwnd, index);
        if (classIcon != nint.Zero)
        {
            Assert.IsTrue(
                WindowsIconTestSupport.HandleContainsSvgLiveEditorBrandPalette(
                    classIcon),
                $"Window class icon {index} was set to non-custom artwork.");
        }
    }

    private static MainViewModel GetViewModel(MainWindow window) =>
        (MainViewModel)window.DataContext;

    private static async Task DeleteDirectoryWithRetriesAsync(string path)
    {
        Exception? failure = null;
        const int attempts = 10;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    return;
                }
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException exception)
            {
                failure = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                failure = exception;
            }

            if (attempt < attempts - 1)
            {
                await Task.Delay(100);
            }
        }

        throw new IOException(
            $"Could not remove icon-test data directory '{path}' after {attempts} attempts.",
            failure);
    }

    private static Exception CombineFailures(
        Exception? existing,
        Exception next) =>
        existing is null
            ? next
            : new AggregateException(existing, next);
}
