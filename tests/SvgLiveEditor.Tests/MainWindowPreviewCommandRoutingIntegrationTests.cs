using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Web.WebView2.Core;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MainWindowPreviewCommandRoutingIntegrationTests
{
    private const string Fixture =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 120\"><rect id=\"rect\" x=\"10\" y=\"10\" width=\"20\" height=\"20\"/><circle id=\"circle\" cx=\"60\" cy=\"20\" r=\"10\"/><ellipse id=\"ellipse\" cx=\"110\" cy=\"20\" rx=\"15\" ry=\"10\"/></svg>";

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public async Task RealPreviewAccelerators_NudgeAndGroupMutateSourceOnce()
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

        Assert.IsTrue(
            await completion.Task.WaitAsync(TimeSpan.FromMinutes(2)));
        Assert.IsTrue(
            thread.Join(TimeSpan.FromSeconds(5)),
            $"The isolated STA did not exit; state={thread.ThreadState}.");
    }

    private static async Task RunHarnessAsync(
        TaskCompletionSource<bool> completion)
    {
        string localApplicationData = Path.Combine(
            Path.GetTempPath(),
            "SvgLiveEditor.CommandRoute.Tests",
            Guid.NewGuid().ToString("N"));
        MainWindow? window = null;
        Exception? failure = null;
        try
        {
            window = new MainWindow(localApplicationData)
            {
                Width = 1100,
                Height = 700,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.ToolWindow,
                Topmost = false
            };
            TaskCompletionSource<CoreWebView2> coreReady = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            window.PreviewWebView.CoreWebView2InitializationCompleted +=
                (_, args) =>
                {
                    if (args.IsSuccess
                        && window.PreviewWebView.CoreWebView2 is CoreWebView2 core)
                    {
                        coreReady.TrySetResult(core);
                    }
                    else
                    {
                        coreReady.TrySetException(
                            args.InitializationException
                            ?? new InvalidOperationException(
                                "WebView2 initialization failed."));
                    }
                };

            window.Show();
            CoreWebView2 core = await coreReady.Task.WaitAsync(
                TimeSpan.FromSeconds(20));
            await WaitForTestWindowReadyAsync(window);
            await SetFixtureAsync(window);

            int previewKeyEvents = 0;
            string? previewOriginalSource = null;
            List<string> routedKeys = [];
            KeyEventHandler recorder = (_, args) =>
            {
                previewKeyEvents++;
                previewOriginalSource = args.OriginalSource?.GetType().Name;
                routedKeys.Add($"{args.Key}/{Keyboard.Modifiers}/{args.Handled}");
            };
            window.PreviewWebView.AddHandler(
                Keyboard.PreviewKeyDownEvent,
                recorder,
                handledEventsToo: true);

            await FocusPreviewAsync(window, core);
            Select(window, "rect");
            SendKey(window.PreviewWebView, VirtualKey.Right);
            await WaitUntilAsync(
                () => window.SourceEditor.Text.Contains(
                    "id=\"rect\" x=\"11\"",
                    StringComparison.Ordinal),
                "Preview Arrow did not nudge the source.",
                () => $"PreviewKeyDown events: {previewKeyEvents}; "
                    + $"original source: {previewOriginalSource ?? "<none>"}; "
                    + $"IsKeyboardFocusWithin: {window.PreviewWebView.IsKeyboardFocusWithin}; "
                    + $"WPF focused element: {Keyboard.FocusedElement?.GetType().Name ?? "<none>"}; "
                    + $"status: {GetViewModel(window).OperationStatus}");
            Assert.AreEqual(1, Count(window.SourceEditor.Text, "x=\"11\""));
            Assert.IsTrue(previewKeyEvents > 0);
            Assert.AreEqual(
                nameof(Microsoft.Web.WebView2.Wpf.WebView2CompositionControl),
                previewOriginalSource);
            AssertSingleUndoRedo(
                window.SourceEditor.Document,
                Fixture,
                window.SourceEditor.Text);

            await SetFixtureAsync(window);
            await FocusPreviewAsync(window, core);
            Select(window, "rect", "circle");
            InvokePrivate(
                window,
                "ApplyPreviewZoomState",
                new PreviewZoomState(PreviewZoomMode.Manual, 5.0));
            await WaitForScriptConditionAsync(
                core,
                "(() => { const v=document.querySelector('.preview-viewport'); return v.scrollHeight > v.clientHeight; })()");
            await core.ExecuteScriptAsync(
                "document.querySelector('.preview-viewport').scrollTop=100");
            double scrollBefore = await ReadScrollTopAsync(core);
            await SendModifiedKeyAsync(
                window.PreviewWebView,
                VirtualKey.Shift,
                VirtualKey.Down);
            await WaitUntilAsync(
                () => window.SourceEditor.Text.Contains(
                    "id=\"rect\" x=\"10\" y=\"20\"",
                    StringComparison.Ordinal)
                && window.SourceEditor.Text.Contains(
                    "id=\"circle\" cx=\"60\" cy=\"30\"",
                    StringComparison.Ordinal),
                "Preview Shift+Arrow did not nudge the complete selection.",
                () => $"Routed keys: {string.Join(", ", routedKeys)}; "
                    + $"status: {GetViewModel(window).OperationStatus}; "
                    + $"selection count: {GetField<SvgMultiSelectionState>(window, "_visualSelectionState").Identities.Count}; "
                    + $"source: {window.SourceEditor.Text}");
            await WaitForVisiblePreviewRevisionAsync(window);
            double scrollAfter = await ReadScrollTopAsync(core);
            Assert.AreEqual(scrollBefore, scrollAfter, 0.5);
            AssertSingleUndoRedo(
                window.SourceEditor.Document,
                Fixture,
                window.SourceEditor.Text);

            await SetFixtureAsync(window);
            await FocusPreviewAsync(window, core);
            Select(window, "rect", "circle");
            await SendModifiedKeyAsync(
                window.PreviewWebView,
                VirtualKey.Control,
                VirtualKey.G);
            await WaitUntilAsync(
                () => window.SourceEditor.Text.Contains(
                    "<g><rect id=\"rect\"",
                    StringComparison.Ordinal),
                "Preview Ctrl+G did not create a real group wrapper.",
                () => $"Routed keys: {string.Join(", ", routedKeys)}; "
                    + $"status: {GetViewModel(window).OperationStatus}; "
                    + $"source: {window.SourceEditor.Text}");
            string grouped = window.SourceEditor.Text;
            Assert.AreEqual(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 120\"><g><rect id=\"rect\" x=\"10\" y=\"10\" width=\"20\" height=\"20\"/><circle id=\"circle\" cx=\"60\" cy=\"20\" r=\"10\"/></g><ellipse id=\"ellipse\" cx=\"110\" cy=\"20\" rx=\"15\" ry=\"10\"/></svg>",
                grouped);
            AssertPrimaryElement(window, "g");
            AssertSingleUndoRedo(window.SourceEditor.Document, Fixture, grouped);
            await WaitForCurrentIndexAsync(window, "g");

            await FocusPreviewAsync(window, core);
            Select(window, "g");
            window.SourceEditor.Document.UndoStack.ClearAll();
            await SendModifiedKeyAsync(
                window.PreviewWebView,
                VirtualKey.Control,
                VirtualKey.Shift,
                VirtualKey.G);
            await WaitUntilAsync(
                () => window.SourceEditor.Text.Equals(
                    Fixture,
                    StringComparison.Ordinal),
                "Preview Ctrl+Shift+G did not remove the neutral wrapper.");
            AssertSingleUndoRedo(window.SourceEditor.Document, grouped, Fixture);

            foreach (CommandSurface surface in new[]
                     {
                         CommandSurface.ArrangeMenu,
                         CommandSurface.ContextMenu
                     })
            {
                await VerifyGroupAndUngroupSurfaceAsync(
                    window,
                    core,
                    surface,
                    grouped);
            }

            await SetFixtureAsync(window);
            await FocusPreviewAsync(window, core);
            Select(window, "rect");
            await SendModifiedKeyAsync(
                window.PreviewWebView,
                VirtualKey.Control,
                VirtualKey.G);
            await WaitUntilAsync(
                () => GetViewModel(window).OperationStatus.Contains(
                    "Select between 2 and 128",
                    StringComparison.Ordinal),
                "An invalid Group shortcut did not explain its rejection.");
            Assert.AreEqual(Fixture, window.SourceEditor.Text);

            SetField(window, "_isPanModeEnabled", true);
            SendKey(window.PreviewWebView, VirtualKey.Down);
            await WaitUntilAsync(
                () => GetViewModel(window).OperationStatus.Contains(
                    "Exit Pan mode",
                    StringComparison.Ordinal),
                "Pan mode did not explain why nudge was rejected.");
            Assert.AreEqual(Fixture, window.SourceEditor.Text);
            SetField(window, "_isPanModeEnabled", false);

            await FocusSourceAsync(window);
            window.SourceEditor.CaretOffset = 0;
            SendKey(window.SourceEditor.TextArea, VirtualKey.Right);
            await WaitUntilAsync(
                () => window.SourceEditor.CaretOffset == 1,
                "Source Arrow input did not remain local to AvalonEdit.",
                () => $"Source focus: {window.SourceEditor.IsKeyboardFocusWithin}; "
                    + $"Preview tracked focus: {GetField<bool>(window, "_isPreviewControllerKeyboardFocused")}; "
                    + $"WPF focused element: {Keyboard.FocusedElement?.GetType().Name ?? "<none>"}");
            Assert.AreEqual(Fixture, window.SourceEditor.Text);

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
                    GetViewModel(window).MarkSaved(string.Empty);
                    window.Close();
                }
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                await TryDeleteAsync(localApplicationData);
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
            if (failure is null)
            {
                completion.TrySetResult(true);
            }
            else
            {
                completion.TrySetException(failure);
            }
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(
                DispatcherPriority.Send);
        }
    }

    private static async Task SetFixtureAsync(MainWindow window)
    {
        window.SourceEditor.Text = Fixture;
        await WaitForCurrentIndexAsync(window, "ellipse");
        window.SourceEditor.Document.UndoStack.ClearAll();
    }

    private static async Task VerifyGroupAndUngroupSurfaceAsync(
        MainWindow window,
        CoreWebView2 core,
        CommandSurface surface,
        string expectedGrouped)
    {
        await SetFixtureAsync(window);
        await FocusSurfaceAsync(window, core, surface);
        Select(window, "rect", "circle");
        await InvokeCompositionCommandAsync(window, surface, ungroup: false);
        await WaitUntilAsync(
            () => window.SourceEditor.Text.Equals(
                expectedGrouped,
                StringComparison.Ordinal),
            $"{surface} Group did not create the expected wrapper.",
            () => $"status: {GetViewModel(window).OperationStatus}; "
                + $"source: {window.SourceEditor.Text}; "
                + DescribeWindowState(window));
        AssertPrimaryElement(window, "g");
        AssertSingleUndoRedo(
            window.SourceEditor.Document,
            Fixture,
            expectedGrouped);
        await WaitForCurrentIndexAsync(window, "g");

        await FocusSurfaceAsync(window, core, surface);
        Select(window, "g");
        window.SourceEditor.Document.UndoStack.ClearAll();
        await InvokeCompositionCommandAsync(window, surface, ungroup: true);
        await WaitUntilAsync(
            () => window.SourceEditor.Text.Equals(
                Fixture,
                StringComparison.Ordinal),
            $"{surface} Ungroup did not remove the neutral wrapper.");
        AssertSingleUndoRedo(
            window.SourceEditor.Document,
            expectedGrouped,
            Fixture);
    }

    private static async Task FocusSurfaceAsync(
        MainWindow window,
        CoreWebView2 core,
        CommandSurface surface)
    {
        await WaitForTestWindowReadyAsync(window);
    }

    private static Task InvokeCompositionCommandAsync(
        MainWindow window,
        CommandSurface surface,
        bool ungroup)
    {
        MenuItem item = surface == CommandSurface.ArrangeMenu
            ? ungroup
                ? window.UngroupMenuItem
                : window.GroupMenuItem
            : window.LayersTree.ContextMenu.Items
                .OfType<MenuItem>()
                .Single(candidate => candidate.Tag as string
                    == (ungroup ? "Ungroup" : "Group"));
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
        return Task.CompletedTask;
    }

    private static async Task WaitForCurrentIndexAsync(
        MainWindow window,
        string expectedElement)
    {
        await WaitUntilAsync(
            () => GetViewModel(window).IsSvgValid
                && GetField<bool>(window, "_isInspectorIndexCurrent")
                && GetField<long>(window, "_inspectorSourceRevision")
                    == GetField<SourceRevisionTracker>(
                        window,
                        "_sourceRevisionTracker").Current
                && GetViewModel(window).Inspector.DocumentIndex?.Elements
                    .Any(element => element.Id == expectedElement
                        || element.Name == expectedElement) == true,
            "The isolated SVG fixture was not indexed.");
    }

    private static void Select(MainWindow window, params string[] ids)
    {
        MainViewModel viewModel = GetViewModel(window);
        SvgDocumentIndex document = viewModel.Inspector.DocumentIndex!;
        long revision = GetField<SourceRevisionTracker>(
            window,
            "_sourceRevisionTracker").Current;
        SvgElementIdentity[] identities = ids
            .Select(id => document.Elements.Single(element =>
                element.Id == id || element.Name == id))
            .Select(element => element.Identity)
            .ToArray();
        SvgMultiSelectionState state = new(
            revision,
            identities,
            identities[^1],
            identities[0]);
        InvokePrivate(
            window,
            "ApplyVisualSelectionState",
            state,
            InspectorSelectionOrigin.PreviewNavigation,
            false);
    }

    private static void AssertPrimaryElement(MainWindow window, string name)
    {
        SvgMultiSelectionState state = GetField<SvgMultiSelectionState>(
            window,
            "_visualSelectionState");
        SvgDocumentIndex document = GetViewModel(window).Inspector.DocumentIndex!;
        Assert.IsNotNull(state.Primary);
        Assert.AreEqual(name, document.FindBestMatch(state.Primary)!.Name);
    }

    private static async Task FocusPreviewAsync(
        MainWindow window,
        CoreWebView2 core)
    {
        await WaitForTestWindowReadyAsync(window);
        RouteApplicationKeyboardFocus(window.PreviewWebView);
        await core.ExecuteScriptAsync(
            "document.querySelector('.preview-viewport').focus({preventScroll:true})");
        await Dispatcher.Yield(DispatcherPriority.Input);
    }

    private static async Task FocusSourceAsync(MainWindow window)
    {
        await WaitForTestWindowReadyAsync(window);
        RouteApplicationKeyboardFocus(window.SourceEditor.TextArea);
        await WaitUntilAsync(
            () => !GetField<bool>(
                window,
                "_isPreviewControllerKeyboardFocused"),
            "The Source focus route did not clear Preview controller focus.",
            () => DescribeWindowState(window));
    }

    private static async Task WaitForTestWindowReadyAsync(MainWindow window)
    {
        await Dispatcher.Yield(DispatcherPriority.Loaded);
        await Dispatcher.Yield(DispatcherPriority.Render);
        await WaitUntilAsync(
            () => window.IsLoaded
                && PresentationSource.FromVisual(window) is HwndSource
                && new WindowInteropHelper(window).Handle != nint.Zero
                && IsWindow(new WindowInteropHelper(window).Handle)
                && window.PreviewWebView.IsLoaded
                && window.PreviewWebView.CoreWebView2 is not null
                && window.PreviewWebView.ActualWidth > 0
                && window.PreviewWebView.ActualHeight > 0,
            "The isolated WPF/WebView2 test window did not become ready.",
            () => DescribeWindowState(window));
    }

    private static void RouteApplicationKeyboardFocus(UIElement target)
    {
        // This is WPF focus routing inside the isolated test window. It does
        // not assert or request OS-global foreground ownership.
        KeyboardFocusChangedEventArgs args = new(
            Keyboard.PrimaryDevice,
            Environment.TickCount,
            oldFocus: null,
            newFocus: target)
        {
            RoutedEvent = Keyboard.GotKeyboardFocusEvent,
            Source = target
        };
        target.RaiseEvent(args);
    }

    private static async Task<double> ReadScrollTopAsync(CoreWebView2 core)
    {
        string json = await core.ExecuteScriptAsync(
            "document.querySelector('.preview-viewport').scrollTop");
        return System.Text.Json.JsonSerializer.Deserialize<double>(json);
    }

    private static async Task WaitForScriptConditionAsync(
        CoreWebView2 core,
        string script)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            string json = await core.ExecuteScriptAsync(script);
            if (System.Text.Json.JsonSerializer.Deserialize<bool>(json))
            {
                return;
            }
            await Task.Delay(25);
        }
        Assert.Fail($"Timed out waiting for trusted-page condition: {script}");
    }

    private static async Task WaitForVisiblePreviewRevisionAsync(
        MainWindow window)
    {
        await WaitUntilAsync(
            () => GetField<long?>(window, "_visiblePreviewSourceRevision")
                    == GetField<SourceRevisionTracker>(
                        window,
                        "_sourceRevisionTracker").Current
                && GetField<long?>(window, "_activePreviewRevision") is null,
            "The changed source did not become the current visible Preview.");
    }

    private static void AssertSingleUndoRedo(
        TextDocument document,
        string original,
        string changed)
    {
        Assert.AreEqual(changed, document.Text);
        Assert.IsTrue(document.UndoStack.CanUndo);
        document.UndoStack.Undo();
        Assert.AreEqual(original, document.Text);
        Assert.IsFalse(document.UndoStack.CanUndo);
        Assert.IsTrue(document.UndoStack.CanRedo);
        document.UndoStack.Redo();
        Assert.AreEqual(changed, document.Text);
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        string failureMessage,
        Func<string>? diagnostics = null)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
        Assert.IsTrue(
            condition(),
            diagnostics is null
                ? failureMessage
                : $"{failureMessage} {diagnostics()}");
    }

    private static MainViewModel GetViewModel(MainWindow window) =>
        GetField<MainViewModel>(window, "_viewModel");

    private static T GetField<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().Name, name);
        return (T)field.GetValue(target)!;
    }

    private static void SetField<T>(object target, string name, T value)
    {
        FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static object? InvokePrivate(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(candidate =>
                    candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
        return method.Invoke(target, arguments);
    }

    private static int Count(string value, string fragment)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(
                   fragment,
                   offset,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }
        return count;
    }

    private static void SendKey(UIElement target, VirtualKey key)
    {
        RaiseWpfKeySequence(target, key);
    }

    private static async Task SendModifiedKeyAsync(
        UIElement target,
        params VirtualKey[] keys)
    {
        Assert.IsTrue(keys.Length >= 2);
        Key expectedKey = keys[^1] switch
        {
            VirtualKey.Down => Key.Down,
            VirtualKey.G => Key.G,
            _ => throw new ArgumentOutOfRangeException(nameof(keys))
        };
        TaskCompletionSource<bool> delivered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        KeyEventHandler recorder = (_, args) =>
        {
            Key key = args.Key == Key.System ? args.SystemKey : args.Key;
            if (key == expectedKey)
            {
                delivered.TrySetResult(true);
            }
        };
        Window window = Window.GetWindow(target)
            ?? throw new InvalidOperationException(
                "The routed input target is not attached to a Window.");
        window.AddHandler(
            Keyboard.PreviewKeyDownEvent,
            recorder,
            handledEventsToo: true);

        try
        {
            byte[] originalKeyboardState = new byte[256];
            Assert.IsTrue(GetKeyboardState(originalKeyboardState));
            byte[] modifiedKeyboardState = (byte[])originalKeyboardState.Clone();
            foreach (VirtualKey modifier in keys[..^1])
            {
                modifiedKeyboardState[(int)modifier] = 0x80;
            }
            Assert.IsTrue(SetKeyboardState(modifiedKeyboardState));
            foreach (VirtualKey modifier in keys[..^1])
            {
                Assert.IsTrue(
                    (GetKeyState((int)modifier) & 0x8000) != 0,
                    $"The test STA did not retain {modifier} state.");
            }
            VirtualKey key = keys[^1];
            try
            {
                RaiseWpfKeySequence(target, key);
                await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                Assert.IsTrue(SetKeyboardState(originalKeyboardState));
            }
        }
        finally
        {
            window.RemoveHandler(Keyboard.PreviewKeyDownEvent, recorder);
        }
    }

    private static void RaiseWpfKeySequence(
        UIElement target,
        VirtualKey virtualKey)
    {
        // WebView2 documents CoreWebView2Controller_AcceleratorKeyPressed as
        // forwarding this exact PreviewKeyDown -> KeyDown routed sequence.
        // Raising it at the real control boundary exercises MainWindow's
        // production route without depending on the desktop foreground lock.
        Key key = virtualKey switch
        {
            VirtualKey.Down => Key.Down,
            VirtualKey.Right => Key.Right,
            VirtualKey.G => Key.G,
            _ => throw new ArgumentOutOfRangeException(nameof(virtualKey))
        };
        PresentationSource inputSource = PresentationSource.FromVisual(target)
            ?? throw new InvalidOperationException(
                "The routed input target has no presentation source.");
        KeyEventArgs args = new(
            Keyboard.PrimaryDevice,
            inputSource,
            Environment.TickCount,
            key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
            Source = target
        };
        target.RaiseEvent(args);
        if (!args.Handled)
        {
            KeyEventArgs bubblingArgs = new(
                Keyboard.PrimaryDevice,
                inputSource,
                Environment.TickCount,
                key)
            {
                RoutedEvent = Keyboard.KeyDownEvent,
                Source = target
            };
            target.RaiseEvent(bubblingArgs);
        }
    }

    private static string DescribeWindowState(MainWindow window)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        uint processId = 0;
        uint windowThread = 0;
        if (handle != nint.Zero)
        {
            windowThread = GetWindowThreadProcessId(handle, out processId);
        }
        return $"HWND=0x{handle:X}; IsWindow={IsWindow(handle)}; "
            + $"loaded={window.IsLoaded}; visible={window.IsVisible}; "
            + $"active={window.IsActive}; foreground=0x{GetForegroundWindow():X}; "
            + $"activeHWND=0x{GetActiveWindow():X}; "
            + $"windowThread={windowThread}; currentThread={GetCurrentThreadId()}; "
            + $"process={processId}; currentProcess={Environment.ProcessId}; "
            + $"WPF focus={Keyboard.FocusedElement?.GetType().Name ?? "<none>"}; "
            + $"Preview WPF focus={window.PreviewWebView.IsKeyboardFocusWithin}; "
            + $"Preview core={window.PreviewWebView.CoreWebView2 is not null}";
    }

    private static async Task TryDeleteAsync(string directory)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
                return;
            }
            catch (IOException) when (attempt < 9)
            {
                await Task.Delay(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                await Task.Delay(50);
            }
        }
    }

    private enum VirtualKey : ushort
    {
        Shift = 0x10,
        Control = 0x11,
        G = 0x47,
        Down = 0x28,
        Right = 0x27
    }

    private enum CommandSurface
    {
        ArrangeMenu,
        ContextMenu
    }

    [DllImport("user32.dll", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);

    [DllImport("user32.dll", SetLastError = false)]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = false)]
    private static extern nint GetActiveWindow();

    [DllImport("user32.dll", SetLastError = false)]
    private static extern uint GetWindowThreadProcessId(
        nint window,
        out uint processId);

    [DllImport("kernel32.dll", SetLastError = false)]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(byte[] keyboardState);

    [DllImport("user32.dll", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKeyboardState(byte[] keyboardState);

    [DllImport("user32.dll", SetLastError = false)]
    private static extern short GetKeyState(int virtualKey);
}
