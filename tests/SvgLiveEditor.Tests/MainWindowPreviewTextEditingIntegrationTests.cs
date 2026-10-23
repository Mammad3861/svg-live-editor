using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MainWindowPreviewTextEditingIntegrationTests
{
    private const string Fixture =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 120\"><text id=\"label\" x=\"20\" y=\"55\" font-size=\"24\">Hello فارسی</text><rect id=\"rect\" x=\"200\" y=\"20\" width=\"40\" height=\"40\"/></svg>";

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    [DataRow("preview", "cancel")]
    [DataRow("properties", "cancel")]
    [DataRow("preview", "save")]
    [DataRow("properties", "save")]
    [DataRow("preview", "discard")]
    [DataRow("properties", "discard")]
    [DataRow("preview", "save-as-cancel")]
    [DataRow("properties", "save-as-cancel")]
    public async Task PendingTextCloseHonorsTheUnsavedDecision(string editor, string decision)
    {
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            _ = RunPendingTextCloseAsync(completion, editor, decision);
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(await completion.Task.WaitAsync(TimeSpan.FromSeconds(40)));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    [DataRow("preview")]
    [DataRow("properties")]
    public Task PendingTextCloseSaveFailureKeepsDocumentOpen(string editor) =>
        PendingTextCloseHonorsTheUnsavedDecision(editor, "save-failure");

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public Task PreviewCloseTimeoutRetainsTextAndRejectsLateReplyOnRetry() =>
        PendingTextCloseHonorsTheUnsavedDecision("preview", "timeout");

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public Task RepeatedPreviewCloseHasOneDecisionAndRejectsInvalidatedReply() =>
        PendingTextCloseHonorsTheUnsavedDecision("preview", "repeated");

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    [DataRow("preview", "cancel", "new")]
    [DataRow("properties", "cancel", "new")]
    [DataRow("preview", "save", "new")]
    [DataRow("properties", "discard", "new")]
    [DataRow("properties", "save-as-cancel", "new")]
    [DataRow("properties", "save-failure", "new")]
    [DataRow("preview", "timeout", "new")]
    [DataRow("preview", "chooser-cancel", "open")]
    [DataRow("properties", "chooser-cancel", "template")]
    public async Task PendingTextReplacementHonorsTheDecision(string editor, string decision, string command)
    {
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            _ = RunPendingTextCloseAsync(completion, editor, decision, command);
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(await completion.Task.WaitAsync(TimeSpan.FromSeconds(40)));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
    }

    private static async Task RunPendingTextCloseAsync(
        TaskCompletionSource<bool> completion, string editor, string decision, string command = "close")
    {
        string profile = Path.Combine(Path.GetTempPath(), "SvgLiveEditor.Close.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(profile, "document.svg");
        MainWindow? window = null;
        bool closed = false;
        TaskCompletionSource<bool> decisionFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> browserExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? failure = null;
        using CancellationTokenSource stopDialogs = new();
        Task? dialogs = null;
        try
        {
            Directory.CreateDirectory(profile);
            File.WriteAllText(path, Fixture);
            window = new MainWindow(profile) { Width = 1100, Height = 700, ShowInTaskbar = false };
            TaskCompletionSource<CoreWebView2> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PreviewWebView.CoreWebView2InitializationCompleted += (_, e) =>
            {
                if (e.IsSuccess) ready.TrySetResult(window.PreviewWebView.CoreWebView2!);
                else ready.TrySetException(e.InitializationException);
            };
            window.Closed += (_, _) => closed = true;
            window.Show();
            CoreWebView2 core = await ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
            uint browserId = core.BrowserProcessId;
            core.Environment.BrowserProcessExited += (_, e) =>
            {
                if (e.BrowserProcessId == browserId) browserExited.TrySetResult(true);
            };
            await SetFixtureAsync(window);
            GetViewModel(window).MarkSaved(decision == "save-as-cancel" ? string.Empty : path);
            Select(window, "label");
            const string value = "Closing <متن> & exact";
            string expected = Fixture.Replace("Hello فارسی", "Closing &lt;متن&gt; &amp; exact", StringComparison.Ordinal);
            if (editor == "preview")
            {
                await OpenPreviewEditorAsync(window, core, useDoubleClick: false);
                await SetEditorValueAsync(core, value);
            }
            else
            {
                window.InspectorTextContentBox.Focus();
                window.InspectorTextContentBox.Text = value;
                window.InspectorTextContentBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
            Assert.AreEqual(Fixture, window.SourceEditor.Text, "The candidate must still be pending.");
            Assert.IsFalse(GetViewModel(window).IsModified);
            if (command != "close")
            {
                await VerifyPendingTextReplacementAsync(window, core, path, expected, decision, command);
                return;
            }
            if (decision is "timeout" or "repeated")
            {
                await VerifyDelayedPreviewCloseAsync(window, core, value, expected, decision);
                return;
            }
            if (decision == "save-failure") File.SetAttributes(path, FileAttributes.ReadOnly);

            // Keep a broken implementation from disposing the test editor before reporting
            // that it would bypass the decision. This handler never supplies the decision.
            bool bypassedDecision = false;
            window.Closing += (_, args) =>
            {
                if (!args.Cancel && window.SourceEditor.Text != expected)
                {
                    bypassedDecision = true;
                    args.Cancel = true;
                }
                if (window.SourceEditor.Text == expected) decisionFinished.TrySetResult(true);
            };
            nint owner = new WindowInteropHelper(window).Handle;
            dialogs = Task.Run(() => RespondToCloseDialogsAsync(owner, decision, stopDialogs.Token));
            window.Close();
            Assert.IsFalse(bypassedDecision, "Closing allowed an unmodified Source to bypass the unsaved decision while text was pending.");
            await dialogs.WaitAsync(TimeSpan.FromSeconds(10));
            await decisionFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(expected, window.SourceEditor.Text, "The close decision must account for the intended text.");
            bool shouldClose = decision is "save" or "discard";
            await WaitUntilAsync(() => closed == shouldClose, "Unexpected close outcome.");
            Assert.AreEqual(decision == "save" ? expected : Fixture, File.ReadAllText(path));
            if (!shouldClose)
            {
                Assert.IsTrue(window.IsVisible);
                Assert.IsTrue(GetViewModel(window).IsModified);
                Assert.IsFalse(GetField<bool>(window, "_isWindowClosing"));
            }
        }
        catch (Exception exception)
        {
            failure = new InvalidOperationException(
                $"Document scenario {command}/{editor}/{decision}; closed={closed}; "
                + $"status={(window?.DataContext is MainViewModel vm ? vm.OperationStatus : "unavailable")}; "
                + $"source={window?.SourceEditor.Text}", exception);
        }
        finally
        {
            stopDialogs.Cancel();
            if (dialogs is not null)
            {
                try { await dialogs; }
                catch (OperationCanceledException) { }
                catch (Exception exception) { failure ??= exception; }
            }
            try
            {
                if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
                if (!closed && window is not null)
                {
                    InvokePrivate(window, "ClosePreviewTextEditSession", false);
                    GetViewModel(window).Inspector.TextContent?.Revert();
                    await DesktopTestWindowCleanup.CloseAndDeleteProfileAsync(window, profile);
                }
                else
                {
                    await browserExited.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    await DesktopTestWindowCleanup.CloseAndDeleteProfileAsync(null, profile);
                }
            }
            catch (Exception exception) { failure ??= exception; }
            if (failure is null) completion.TrySetResult(true);
            else completion.TrySetException(failure);
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        }
    }

    private static async Task VerifyPendingTextReplacementAsync(
        MainWindow window, CoreWebView2 core, string path, string expected, string decision, string command)
    {
        long session = GetField<long>(window, "_documentSession");
        string? originalPath = GetViewModel(window).CurrentFilePath;
        if (decision == "chooser-cancel")
        {
            if (command == "open")
            {
                nint owner = new WindowInteropHelper(window).Handle;
                Task chooser = Task.Run(() => RespondToCloseDialogsAsync(owner, "open-cancel", CancellationToken.None));
                InvokePrivate(window, "OnOpenClick", window, new RoutedEventArgs());
                await chooser.WaitAsync(TimeSpan.FromSeconds(10));
            }
            else
            {
                DispatcherOperation chooser = window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    TemplateGalleryWindow gallery = window.OwnedWindows.OfType<TemplateGalleryWindow>().Single();
                    gallery.DialogResult = false;
                }));
                InvokePrivate(window, "OnNewFromTemplateClick", window, new RoutedEventArgs());
                await chooser.Task;
            }
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(session, GetField<long>(window, "_documentSession"));
            Assert.AreEqual(originalPath, GetViewModel(window).CurrentFilePath);
            Assert.AreEqual(Fixture, File.ReadAllText(path));
            if (window.SourceEditor.Text == Fixture)
            {
                if (command == "open")
                    Assert.IsTrue(await ReadScriptBooleanAsync(core,
                        "document.querySelector('.text-editor-input')?.value === 'Closing <متن> & exact'"));
                else
                    Assert.AreEqual("Closing <متن> & exact", GetViewModel(window).Inspector.TextContent!.Value);
            }
            else Assert.AreEqual(expected, window.SourceEditor.Text);
            return;
        }

        if (decision == "timeout")
        {
            await core.ExecuteScriptAsync("""
                window.closeTestReplies = [];
                window.closeTestPost = window.chrome.webview.postMessage.bind(window.chrome.webview);
                window.chrome.webview.postMessage = message => {
                  if (message.type === 'textEdit' && message.action === 'commit') {
                    window.closeTestReplies.push(message);
                  } else {
                    window.closeTestPost(message);
                  }
                };
                """);
            InvokePrivate(window, "OnNewClick", window, new RoutedEventArgs());
            object? pending = GetField<object?>(window, "_pendingPreviewTextEditCommit");
            Assert.IsNotNull(pending);
            InvokePrivate(window, "OnNewClick", window, new RoutedEventArgs());
            window.Close();
            Assert.AreSame(pending, GetField<object?>(window, "_pendingPreviewTextEditCommit"));
            await WaitForScriptConditionAsync(core, "window.closeTestReplies.length === 1");
            await WaitUntilAsync(() => !GetField<bool>(window, "_isPreparingTextEditClose"), "Unanswered New did not time out.");
            Assert.AreEqual(session, GetField<long>(window, "_documentSession"));
            Assert.AreEqual(Fixture, window.SourceEditor.Text);
            Assert.AreEqual(originalPath, GetViewModel(window).CurrentFilePath);
            Assert.IsTrue(window.IsVisible);
            await WaitForScriptConditionAsync(core,
                "document.querySelector('.text-editor-input')?.value === 'Closing <متن> & exact' && !document.querySelector('.text-editor-input').disabled");
            await SetEditorValueAsync(core, "Replacement <جدید>");
            InvokePrivate(window, "OnNewClick", window, new RoutedEventArgs());
            await WaitForScriptConditionAsync(core, "window.closeTestReplies.length === 2");
            await ReleaseHeldCloseReplyAsync(core, 0);
            Assert.AreEqual(Fixture, window.SourceEditor.Text);
            Assert.AreEqual(session, GetField<long>(window, "_documentSession"));
            Assert.IsTrue(GetField<bool>(window, "_isPreparingTextEditClose"));
        }

        string? delayedReply = decision == "timeout"
            ? JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync("JSON.stringify(window.closeTestReplies[0])"))
            : null;
        using CancellationTokenSource stop = new();
        nint dialogOwner = new WindowInteropHelper(window).Handle;
        Task dialogs = Task.Run(() => RespondToCloseDialogsAsync(dialogOwner,
            decision == "timeout" ? "discard" : decision, stop.Token));
        if (decision == "save-failure") File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            if (decision == "timeout") await ReleaseHeldCloseReplyAsync(core, 1);
            else InvokePrivate(window, "OnNewClick", window, new RoutedEventArgs());
            if (decision is "cancel" or "save-as-cancel" or "save-failure")
                Assert.AreEqual(originalPath, GetViewModel(window).CurrentFilePath,
                    "New replaced the document before resolving pending text.");
            await dialogs.WaitAsync(TimeSpan.FromSeconds(10));
            await WaitUntilAsync(() => !GetField<bool>(window, "_isPreparingTextEditClose"), "New did not finish its decision.");
            bool replaced = decision is "save" or "discard" or "timeout";
            string welcome = GetField<WelcomeSvgProvider>(window, "_welcomeSvgProvider").Load();
            Assert.AreEqual(replaced ? welcome : expected, window.SourceEditor.Text);
            Assert.AreEqual(replaced ? session + 1 : session, GetField<long>(window, "_documentSession"));
            Assert.AreEqual(decision == "save" ? expected : Fixture, File.ReadAllText(path));
            Assert.IsTrue(window.IsVisible);
            if (delayedReply is not null)
            {
                await WaitUntilAsync(() => GetField<long?>(window, "_visiblePreviewSourceRevision") == GetRevision(window),
                    "The replacement Preview did not become current.");
                await ReleaseHeldCloseReplyAsync(core, 0, delayedReply);
                Assert.AreEqual(welcome, window.SourceEditor.Text, "An old reply edited the replacement document.");
                Assert.AreEqual(session + 1, GetField<long>(window, "_documentSession"));
            }
        }
        finally
        {
            stop.Cancel();
            try { await dialogs; }
            catch (OperationCanceledException) { }
        }
    }

    private static async Task VerifyDelayedPreviewCloseAsync(
        MainWindow window, CoreWebView2 core, string value, string expected, string scenario)
    {
        // Delay only app-owned commit messages; the WebView and its input stay alive.
        await core.ExecuteScriptAsync("""
            window.closeTestReplies = [];
            window.closeTestPost = window.chrome.webview.postMessage.bind(window.chrome.webview);
            window.chrome.webview.postMessage = message => {
              if (message.type === 'textEdit' && message.action === 'commit') {
                window.closeTestReplies.push(message);
              } else {
                window.closeTestPost(message);
              }
            };
            """);
        window.Close();
        object? pending = GetField<object?>(window, "_pendingPreviewTextEditCommit");
        Assert.IsNotNull(pending);
        window.Close();
        window.Close();
        Assert.AreSame(pending, GetField<object?>(window, "_pendingPreviewTextEditCommit"));
        await WaitForScriptConditionAsync(core, "window.closeTestReplies.length === 1");
        Assert.AreEqual(Fixture, window.SourceEditor.Text);

        if (scenario == "timeout")
        {
            await WaitUntilAsync(() => !GetField<bool>(window, "_isPreparingTextEditClose"),
                "The unanswered close did not time out.");
            Assert.IsTrue(window.IsVisible);
            Assert.IsFalse(GetField<bool>(window, "_isWindowClosing"));
            Assert.IsNotNull(GetField<object?>(window, "_previewTextEditSession"));
            await WaitForScriptConditionAsync(core,
                $"document.querySelector('.text-editor-input')?.value === {JsonSerializer.Serialize(value)} && !document.querySelector('.text-editor-input').disabled");
            await SetEditorValueAsync(core, "Retry <جدید>");
            expected = Fixture.Replace("Hello فارسی", "Retry &lt;جدید&gt;", StringComparison.Ordinal);
            window.Close();
            await WaitForScriptConditionAsync(core, "window.closeTestReplies.length === 2");
            await ReleaseHeldCloseReplyAsync(core, 0);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(Fixture, window.SourceEditor.Text, "A timed-out reply must not commit the older candidate.");
            Assert.IsTrue(GetField<bool>(window, "_isPreparingTextEditClose"));
        }

        int prompts = 0;
        using CancellationTokenSource stop = new();
        nint owner = new WindowInteropHelper(window).Handle;
        Task dialogs = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                    await RespondToCloseDialogsAsync(owner, "cancel", stop.Token,
                        () => Interlocked.Increment(ref prompts));
            }
            catch (OperationCanceledException) { }
        });
        bool repeatedAtYield = false;
        void RepeatAtYield(object? sender, DispatcherHookEventArgs args)
        {
            if (scenario == "repeated" && !repeatedAtYield
                && args.Operation.Priority == DispatcherPriority.Background
                && window.SourceEditor.Text == expected)
            {
                repeatedAtYield = true;
                window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(window.Close));
            }
        }
        window.Dispatcher.Hooks.OperationPosted += RepeatAtYield;
        try
        {
            await ReleaseHeldCloseReplyAsync(core, -1);
            await WaitUntilAsync(() => prompts >= 1, "The successful retry did not reach the unsaved decision.");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.IsFalse(GetField<bool>(window, "_isPreparingTextEditClose"));
            Assert.IsTrue(window.IsVisible);
            Assert.AreEqual(1, prompts, "Repeated closing produced competing unsaved-change decisions.");
            if (scenario == "repeated") Assert.IsTrue(repeatedAtYield);
            Assert.AreEqual(expected, window.SourceEditor.Text);
            window.SourceEditor.Undo();
            Assert.AreEqual(Fixture, window.SourceEditor.Text);
            Assert.IsFalse(window.SourceEditor.CanUndo, "Only one source commit/Undo unit is allowed.");
            window.SourceEditor.Redo();
            Assert.AreEqual(expected, window.SourceEditor.Text);
            await ReleaseHeldCloseReplyAsync(core, 0);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(expected, window.SourceEditor.Text);
            Assert.AreEqual(1, prompts);
        }
        finally
        {
            window.Dispatcher.Hooks.OperationPosted -= RepeatAtYield;
            stop.Cancel();
            await dialogs;
        }

        if (scenario != "repeated") return;

        // Cancellation must end closing even if its commit reply arrives later.
        await SetFixtureAsync(window);
        Select(window, "label");
        await OpenPreviewEditorAsync(window, core, useDoubleClick: false);
        await SetEditorValueAsync(core, "must not commit");
        await core.ExecuteScriptAsync("window.closeTestReplies = []");
        window.Close();
        await WaitForScriptConditionAsync(core, "window.closeTestReplies.length === 1");
        InvokePrivate(window, "ClosePreviewTextEditSession", false);
        await WaitUntilAsync(() => !GetField<bool>(window, "_isPreparingTextEditClose"), "Cancel did not abandon closing.");
        await ReleaseHeldCloseReplyAsync(core, 0);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Assert.AreEqual(Fixture, window.SourceEditor.Text);
        Assert.IsTrue(window.IsVisible);

        // Independently invalidate another unanswered close by changing Source.
        await WaitForScriptConditionAsync(core, "document.querySelector('.text-editor-input') === null");
        await OpenPreviewEditorAsync(window, core, useDoubleClick: false);
        await SetEditorValueAsync(core, "also must not commit");
        await core.ExecuteScriptAsync("window.closeTestReplies = []");
        window.Close();
        await WaitForScriptConditionAsync(core, "window.closeTestReplies.length === 1");
        string newer = Fixture.Replace("Hello فارسی", "Newer source", StringComparison.Ordinal);
        window.SourceEditor.Text = newer;
        await WaitUntilAsync(() => !GetField<bool>(window, "_isPreparingTextEditClose"), "A source change did not abandon closing.");
        await ReleaseHeldCloseReplyAsync(core, 0);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Assert.AreEqual(newer, window.SourceEditor.Text);
        Assert.IsTrue(window.IsVisible);
        Assert.IsFalse(GetField<bool>(window, "_isWindowClosing"));
    }

    private static async Task ReleaseHeldCloseReplyAsync(CoreWebView2 core, int index, string? messageJson = null)
    {
        TaskCompletionSource<bool> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            using JsonDocument message = JsonDocument.Parse(args.WebMessageAsJson);
            if (message.RootElement.TryGetProperty("type", out JsonElement type)
                && type.GetString() == "textEdit"
                && message.RootElement.GetProperty("action").GetString() == "commit")
            {
                received.TrySetResult(true);
            }
        }
        core.WebMessageReceived += OnMessage;
        try
        {
            string post = messageJson is null
                ? $"window.closeTestPost(window.closeTestReplies.at({index}))"
                : $"window.chrome.webview.postMessage({messageJson})";
            Assert.IsTrue(await ReadScriptBooleanAsync(core, $"(() => {{ {post}; return true; }})()"));
            await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            core.WebMessageReceived -= OnMessage;
        }
    }

    private static async Task RespondToCloseDialogsAsync(nint owner, string decision, CancellationToken cancellationToken, Action? onReply = null)
    {
        (string Title, int Command)[] replies = decision switch
        {
            "open-cancel" => [("Open SVG or text file", 2)],
            "save" => [("Unsaved changes", 6)],
            "discard" => [("Unsaved changes", 7)],
            "save-as-cancel" => [("Unsaved changes", 6), ("Save SVG source", 2)],
            "save-failure" => [("Unsaved changes", 6), ("Cannot save file", 1)],
            _ => [("Unsaved changes", 2)]
        };
        foreach ((string title, int command) in replies)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            bool replied = false;
            HashSet<string> observed = [];
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                nint popup = 0;
                _ = EnumThreadWindows(GetWindowThreadProcessId(owner, out _), (candidate, parameter) =>
                {
                    System.Text.StringBuilder text = new(256);
                    _ = GetWindowText(candidate, text, text.Capacity);
                    observed.Add($"{text}; button1={GetDlgItem(candidate, 1)}; button2={GetDlgItem(candidate, 2)}; button6={GetDlgItem(candidate, 6)}");
                    if (candidate != owner && text.ToString() == title) popup = candidate;
                    return true;
                }, 0);
                nint button = popup == 0 ? 0 : GetDlgItem(popup, command);
                // This runtime exposes the single error-dialog OK button as IDCANCEL.
                if (button == 0 && popup != 0 && title == "Cannot save file")
                {
                    button = GetDlgItem(popup, 2);
                }
                if (button != 0 && IsWindowEnabled(button))
                {
                    Assert.IsTrue(PostMessage(button, 0x00F5, 0, 0));
                    onReply?.Invoke();
                    if (onReply is not null)
                    {
                        while (IsWindow(button)) await Task.Delay(25, cancellationToken);
                    }
                    replied = true;
                    break;
                }
                await Task.Delay(25, cancellationToken);
            }
            Assert.IsTrue(replied, $"The owned '{title}' dialog was not shown. Observed: {string.Join(" | ", observed)}");
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint GetDlgItem(nint dialog, int identifier);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(nint window);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);

    private delegate bool ThreadWindowCallback(nint window, nint parameter);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool EnumThreadWindows(uint threadId, ThreadWindowCallback callback, nint parameter);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetWindowText(nint window, System.Text.StringBuilder text, int maximum);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public async Task RealWindow_PreviewAndPropertiesTextEditsStayAtomicAndSynchronized()
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


    [TestMethod]
    [TestCategory("DesktopIntegration")]
    [DataRow("F2")]
    [DataRow("Enter")]
    [DataRow("captured-double-click")]
    [DataRow("composition-double-click")]
    [DataRow("layers-down")]
    [DataRow("layers-control-enter")]
    [DataRow("preview-focus")]
    public async Task RealInputBoundary_DoesNotDependOnSyntheticFocusOrImageTarget(string route)
    {
        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            _ = RunInputBoundaryAsync(completion, route);
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(await completion.Task.WaitAsync(TimeSpan.FromMinutes(1)));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
    }

    private static async Task RunInputBoundaryAsync(
        TaskCompletionSource<bool> completion, string route)
    {
        string profile = Path.Combine(Path.GetTempPath(),
            "SvgLiveEditor.InputBoundary.Tests", Guid.NewGuid().ToString("N"));
        MainWindow? window = null;
        Exception? failure = null;
        try
        {
            window = new MainWindow(profile) { Width = 1100, Height = 700 };
            TaskCompletionSource<CoreWebView2> ready = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            window.PreviewWebView.CoreWebView2InitializationCompleted += (_, e) =>
            {
                if (e.IsSuccess) ready.TrySetResult(window.PreviewWebView.CoreWebView2!);
                else ready.TrySetException(e.InitializationException);
            };
            window.Show();
            CoreWebView2 core = await ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
            string source = Fixture.Replace("Hello فارسی", "English text", StringComparison.Ordinal);
            window.SourceEditor.Text = source;
            await WaitForSourceAndIndexAsync(window, source);
            await WaitUntilAsync(
                () => GetField<object?>(window, "_pendingPreviewTextMeasurement") is null,
                "Text measurement did not settle.");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            if (route.StartsWith("layers-", StringComparison.Ordinal))
            {
                window.LayersTree.UpdateLayout();
                SvgLayerViewModel first = GetViewModel(window).Inspector.LayerRoots[0];
                SvgLayerViewModel next = GetViewModel(window).Inspector.LayerRoots[1];
                TreeViewItem row = (TreeViewItem)window.LayersTree.ItemContainerGenerator
                    .ContainerFromItem(first);
                Assert.IsTrue(row.Focus(), "The starting tree row must own actual WPF focus.");
                Assert.AreSame(row, Keyboard.FocusedElement);
                if (route == "layers-control-enter")
                {
                    ProcessBoundaryKey(row, Key.Tab);
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    Assert.IsInstanceOfType<Button>(Keyboard.FocusedElement,
                        "Tab must keep deliberate access to layer controls. "
                        + $"tree={KeyboardNavigation.GetTabNavigation(window.LayersTree)} row={KeyboardNavigation.GetTabNavigation(row)}");
                    ProcessBoundaryKey((Button)Keyboard.FocusedElement, Key.Enter);
                    Assert.IsTrue(window.SourceEditor.Text.Contains("display=\"none\"", StringComparison.Ordinal),
                        "Enter on the focused eye button must toggle visibility rather than activate its tree row.");
                    return;
                }
                ProcessBoundaryKey(row, Key.Down);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Assert.AreSame(next, window.LayersTree.SelectedItem,
                    "Down from row focus must select the next row, not an eye button.");
                Assert.IsInstanceOfType<TreeViewItem>(Keyboard.FocusedElement,
                    "Arrow navigation must retain row focus.");
                AssertPrimaryElement(window, next.Element.Id!);
                Assert.AreEqual(source, window.SourceEditor.Text);
            }
            else
            {
                window.PreviewWebView.Focus();
                await Dispatcher.Yield(DispatcherPriority.Input);
                if (route == "preview-focus")
                {
                    Assert.IsTrue(((IKeyboardInputSink)window.PreviewWebView).HasFocusWithin(),
                        "The real native Preview controller must first own focus.");

                }
                // Browser protocol input runs the real trusted pointer handlers, including
                // pointer capture and the host's hit-test/selection response. It does not
                // inject a selection, a controller-focus flag, or a DOM-dispatched event.
                string pointJson = JsonSerializer.Deserialize<string>(
                    await core.ExecuteScriptAsync("""
                    (() => { const r=document.querySelector('img').getBoundingClientRect();
                      return JSON.stringify({x:r.left+r.width*50/300,y:r.top+r.height*47/120}); })()
                    """))!;
                using JsonDocument point = JsonDocument.Parse(pointJson);
                double x = point.RootElement.GetProperty("x").GetDouble();
                double y = point.RootElement.GetProperty("y").GetDouble();
                await DispatchBoundaryClickAsync(core, x, y, 1);
                await WaitUntilAsync(
                    () => GetViewModel(window).Inspector.SelectedElement?.Element.Id == "label",
                    "The actual Preview pointer route did not select the text.");
                await WaitForScriptConditionAsync(core,
                    "document.querySelector('.selection-overlay')?.dataset.state === 'selected'");

                TaskCompletionSource<bool> begin = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                PreviewTextEditMessageParser parser = new();
                string token = GetField<string>(window, "_activePreviewBridgeToken");
                long revision = GetRevision(window);
                core.WebMessageReceived += (_, e) =>
                {
                    if (parser.TryParse(e.WebMessageAsJson, token, revision, out var message)
                        && message.Action == PreviewTextEditAction.Begin)
                        begin.TrySetResult(true);
                };
                if (route.EndsWith("double-click", StringComparison.Ordinal))
                {
                    await DispatchBoundaryClickAsync(core, x, y,
                        route == "composition-double-click" ? 3 : 2);
                }
                else
                {
                    // WPF composition input supplies key/virtual-key with an empty code.
                    // Assert the page-to-host boundary independently of desktop activation.
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",
                        JsonSerializer.Serialize(new
                        {
                            type = "keyDown", key = route == "preview-focus" ? "F2" : route,
                            code = route == "preview-focus" ? "F2" : string.Empty,
                            windowsVirtualKeyCode = route is "F2" or "preview-focus" ? 113 : 13
                        }));
                }
                try { Assert.IsTrue(await begin.Task.WaitAsync(TimeSpan.FromSeconds(3))); }
                catch (TimeoutException)
                {
                    Assert.Fail($"{route} did not request a validated text-edit begin through the real page input route.");
                }
                await WaitForScriptConditionAsync(core,
                    "document.activeElement?.classList.contains('text-editor-input') === true");
                Assert.IsNotNull(GetField<object?>(window, "_previewTextEditSession"),
                    "Real pointer/keyboard entry must create the host session without test-established focus flags.");
                Assert.AreEqual(source, window.SourceEditor.Text);
            }
        }
        catch (Exception e) { failure = e; }
        finally
        {
            try
            {
                await DesktopTestWindowCleanup.CloseAndDeleteProfileAsync(window, profile);
            }
            catch (Exception exception)
            {
                failure = failure is null
                    ? exception
                    : new AggregateException(failure, exception);
            }
            if (failure is null) completion.TrySetResult(true);
            else completion.TrySetException(failure);
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        }
    }

    private static async Task DispatchBoundaryClickAsync(
        CoreWebView2 core, double x, double y, int count)
    {
        foreach (string type in new[] { "mousePressed", "mouseReleased" })
        {
            await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                JsonSerializer.Serialize(new
                {
                    type, x, y, button = "left",
                    buttons = type == "mousePressed" ? 1 : 0,
                    clickCount = count, pointerType = "mouse"
                }));
        }
    }

    private static void ProcessBoundaryKey(UIElement target, Key key)
    {
        PresentationSource inputSource = PresentationSource.FromVisual(target)!;
        KeyEventArgs preview = new(Keyboard.PrimaryDevice, inputSource,
            Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = target
        };
        target.RaiseEvent(preview);
        if (!preview.Handled)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, inputSource, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.KeyDownEvent, Source = target
            });
        }
    }

    private static async Task RunHarnessAsync(
        TaskCompletionSource<bool> completion)
    {
        string localApplicationData = Path.Combine(
            Path.GetTempPath(),
            "SvgLiveEditor.TextEdit.Tests",
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
                        && window.PreviewWebView.CoreWebView2
                            is CoreWebView2 core)
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

            await SetFixtureAsync(window);
            Select(window, "label");
            await OpenPreviewEditorAsync(window, core, useDoubleClick: false);

            const string previewValue = "A & B < C فارسی";
            await SetEditorValueAsync(core, "ignored while composing");
            await core.ExecuteScriptAsync(
                "document.querySelector('.text-editor-input').dispatchEvent(new CompositionEvent('compositionstart',{bubbles:true,data:'x'}))");
            await PressEditorKeyAsync(core, "Enter");
            Assert.AreEqual(Fixture, window.SourceEditor.Text);
            Assert.IsTrue(await ReadScriptBooleanAsync(
                core,
                "document.querySelector('.text-editor-input') !== null"));
            await core.ExecuteScriptAsync(
                "document.querySelector('.text-editor-input').dispatchEvent(new CompositionEvent('compositionend',{bubbles:true,data:'x'}))");
            await SetEditorValueAsync(core, previewValue);
            await PressEditorKeyAsync(core, "Enter");

            string previewEdited = Fixture.Replace(
                "Hello فارسی",
                "A &amp; B &lt; C فارسی",
                StringComparison.Ordinal);
            await WaitForSourceAndIndexAsync(window, previewEdited);
            AssertSynchronizedTextSelection(window, previewValue);
            Assert.IsTrue(GetViewModel(window).IsModified);
            AssertSingleDocumentUndoRedo(
                window,
                Fixture,
                previewEdited,
                "Hello فارسی",
                previewValue);

            await SetFixtureAsync(window);
            Select(window, "label");
            const string propertyValue = "Properties <exact> فارسی & test";
            TextBox textBox = window.InspectorTextContentBox;
            RouteApplicationKeyboardFocus(textBox);
            textBox.Text = propertyValue;
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            RaiseKey(textBox, Key.Enter);

            string propertyEdited = Fixture.Replace(
                "Hello فارسی",
                "Properties &lt;exact&gt; فارسی &amp; test",
                StringComparison.Ordinal);
            await WaitForSourceAndIndexAsync(window, propertyEdited);
            AssertSynchronizedTextSelection(window, propertyValue);
            AssertSingleDocumentUndoRedo(
                window,
                Fixture,
                propertyEdited,
                "Hello فارسی",
                propertyValue);

            string beforeEscape = window.SourceEditor.Text;
            textBox.Text = "cancel this";
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            RaiseKey(textBox, Key.Escape);
            Assert.AreEqual(beforeEscape, window.SourceEditor.Text);
            Assert.AreEqual(
                propertyValue,
                GetViewModel(window).Inspector.TextContent!.Value);

            await SetFixtureAsync(window);
            Select(window, "label");
            await OpenPreviewEditorAsync(window, core, useDoubleClick: true);
            const string blurValue = "Blur commit فارسی";
            await SetEditorValueAsync(core, blurValue);
            await core.ExecuteScriptAsync(
                "document.querySelector('.text-editor-input').blur()");
            string blurEdited = Fixture.Replace(
                "Hello فارسی",
                blurValue,
                StringComparison.Ordinal);
            await WaitForSourceAndIndexAsync(window, blurEdited);
            AssertSynchronizedTextSelection(window, blurValue);

            await SetFixtureAsync(window);
            Select(window, "label");
            await OpenPreviewEditorAsync(window, core, useDoubleClick: false);
            Select(window, "rect");
            await WaitForScriptConditionAsync(
                core,
                "document.querySelector('.text-editor-input') === null");
            Assert.AreEqual(Fixture, window.SourceEditor.Text);
            AssertPrimaryElement(window, "rect");

            const string inheritedXmlSpace =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 120\"><g xml:space=\"preserve\"><text id=\"label\" x=\"20\" y=\"55\" font-size=\"24\">A   B</text></g></svg>";
            window.SourceEditor.Text = inheritedXmlSpace;
            await WaitForSourceAndIndexAsync(window, inheritedXmlSpace);
            await WaitUntilAsync(
                () => GetField<object?>(
                    window,
                    "_pendingPreviewTextMeasurement") is null,
                "The inherited xml:space measurement decision did not settle.");
            window.SourceEditor.Document.UndoStack.ClearAll();
            window.SourceEditor.Document.UndoStack.MarkAsOriginalFile();
            GetViewModel(window).MarkSaved(string.Empty);
            Select(window, "label");
            RouteApplicationKeyboardFocus(window.PreviewWebView);
            SvgVisualDocument visible = GetField<SvgVisualDocument>(
                window,
                "_visiblePreviewVisualDocument");
            SvgVisualElement unsupported = visible.Elements.Single(element =>
                element.SourceElement.Id == "label");
            string selectionId = GetField<string>(
                window,
                "_visualSelectionBridgeId");
            long revision = GetRevision(window);

            Assert.IsNull(unsupported.TextMeasurement);
            Assert.IsNull(unsupported.Geometry);
            Assert.IsFalse(unsupported.IsResizable);
            Assert.IsFalse(
                GetViewModel(window).Inspector.TextContent!.IsEnabled);
            InvokePrivate(
                window,
                "BeginPreviewTextEdit",
                new PreviewTextEditMessage(
                    PreviewTextEditAction.Begin,
                    revision,
                    selectionId,
                    SessionId: null,
                    Value: null));
            InvokePrivate(
                window,
                "HandlePreviewTextEdit",
                new PreviewTextEditMessage(
                    PreviewTextEditAction.Commit,
                    revision,
                    selectionId,
                    SessionId: Guid.NewGuid().ToString("N"),
                    Value: "replacement"));

            Assert.IsNull(GetField<object?>(window, "_previewTextEditSession"));
            Assert.AreEqual(inheritedXmlSpace, window.SourceEditor.Text);
            Assert.IsFalse(window.SourceEditor.CanUndo);
            Assert.IsFalse(GetViewModel(window).IsModified);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            try
            {
                await DesktopTestWindowCleanup.CloseAndDeleteProfileAsync(window, localApplicationData);
            }
            catch (Exception exception)
            {
                failure = failure is null
                    ? exception
                    : new AggregateException(failure, exception);
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
        await WaitForSourceAndIndexAsync(window, Fixture);
        window.SourceEditor.Document.UndoStack.ClearAll();
        window.SourceEditor.Document.UndoStack.MarkAsOriginalFile();
        GetViewModel(window).MarkSaved(string.Empty);
    }

    private static async Task WaitForSourceAndIndexAsync(
        MainWindow window,
        string expectedSource)
    {
        await WaitUntilAsync(
            () => window.SourceEditor.Text.Equals(
                    expectedSource,
                    StringComparison.Ordinal)
                && GetViewModel(window).IsSvgValid
                && GetField<bool>(window, "_isInspectorIndexCurrent")
                && GetField<long>(window, "_inspectorSourceRevision")
                    == GetRevision(window)
                && GetField<long?>(window, "_visiblePreviewSourceRevision")
                    == GetRevision(window)
                && GetViewModel(window).Inspector.DocumentIndex?.Elements
                    .Any(element => element.Id == "label") == true,
            "The text fixture did not become the current validated source and Preview.",
            () => $"revision={GetRevision(window)}; "
                + $"inspector={GetField<long>(window, "_inspectorSourceRevision")}; "
                + $"visible={GetField<long?>(window, "_visiblePreviewSourceRevision")}; "
                + $"status={GetViewModel(window).ValidationStatus}; "
                + $"operation={GetViewModel(window).OperationStatus}");
    }

    private static async Task OpenPreviewEditorAsync(
        MainWindow window,
        CoreWebView2 core,
        bool useDoubleClick)
    {
        RouteApplicationKeyboardFocus(window.PreviewWebView);
        await core.ExecuteScriptAsync(
            "document.querySelector('.preview-viewport').focus({preventScroll:true})");
        await WaitForScriptConditionAsync(
            core,
            "document.querySelector('.selection-overlay')?.dataset.state === 'selected'");
        string script = useDoubleClick
            ? "document.querySelector('img').dispatchEvent(new MouseEvent('dblclick',{bubbles:true,cancelable:true,button:0}))"
            : "window.dispatchEvent(new KeyboardEvent('keydown',{bubbles:true,cancelable:true,code:'F2',key:'F2'}))";
        await core.ExecuteScriptAsync(script);
        await WaitForScriptConditionAsync(
            core,
            "document.activeElement?.classList.contains('text-editor-input') === true");
        Assert.IsNotNull(GetField<object?>(window, "_previewTextEditSession"));
    }

    private static async Task SetEditorValueAsync(
        CoreWebView2 core,
        string value)
    {
        string serialized = JsonSerializer.Serialize(value);
        await core.ExecuteScriptAsync(
            $"document.querySelector('.text-editor-input').value={serialized}");
    }

    private static Task PressEditorKeyAsync(CoreWebView2 core, string key) =>
        core.ExecuteScriptAsync(
            $"document.querySelector('.text-editor-input').dispatchEvent(new KeyboardEvent('keydown',{{bubbles:true,cancelable:true,code:'{key}',key:'{key}'}}))");

    private static void AssertSingleDocumentUndoRedo(
        MainWindow window,
        string original,
        string changed,
        string originalText,
        string changedText)
    {
        Assert.AreEqual(changed, window.SourceEditor.Text);
        Assert.IsTrue(window.SourceEditor.CanUndo);
        InvokePrivate(window, "OnUndoClick", window, new RoutedEventArgs());
        Assert.AreEqual(original, window.SourceEditor.Text);
        Assert.IsFalse(window.SourceEditor.CanUndo);
        ApplyCurrentValidation(window);
        AssertSynchronizedTextSelection(window, originalText);

        Assert.IsTrue(window.SourceEditor.CanRedo);
        InvokePrivate(window, "OnRedoClick", window, new RoutedEventArgs());
        Assert.AreEqual(changed, window.SourceEditor.Text);
        ApplyCurrentValidation(window);
        AssertSynchronizedTextSelection(window, changedText);
    }

    private static void ApplyCurrentValidation(MainWindow window)
    {
        string source = window.SourceEditor.Text;
        long revision = GetRevision(window);
        SvgDocumentIndexResult result =
            new SvgDocumentIndexService().Build(source);
        InvokePrivate(
            window,
            "ApplyValidationResult",
            source,
            revision,
            result,
            null!,
            false);
    }

    private static void AssertSynchronizedTextSelection(
        MainWindow window,
        string expectedText)
    {
        AssertPrimaryElement(window, "label");
        MainViewModel viewModel = GetViewModel(window);
        Assert.AreEqual(
            "label",
            viewModel.Inspector.SelectedElement?.Element.Id);
        Assert.IsNotNull(viewModel.Inspector.TextContent);
        Assert.AreEqual(expectedText, viewModel.Inspector.TextContent.Value);
    }

    private static void Select(MainWindow window, string id)
    {
        MainViewModel viewModel = GetViewModel(window);
        SvgDocumentIndex document = viewModel.Inspector.DocumentIndex!;
        SvgElementIdentity identity = document.Elements.Single(element =>
            element.Id == id).Identity;
        SvgMultiSelectionState state = new(
            GetRevision(window),
            [identity],
            identity,
            identity);
        InvokePrivate(
            window,
            "ApplyVisualSelectionState",
            state,
            InspectorSelectionOrigin.PreviewNavigation,
            false);
    }

    private static void AssertPrimaryElement(MainWindow window, string id)
    {
        SvgMultiSelectionState state = GetField<SvgMultiSelectionState>(
            window,
            "_visualSelectionState");
        SvgElementNode? primary = state.Primary is null
            ? null
            : GetViewModel(window).Inspector.DocumentIndex?
                .FindBestMatch(state.Primary);
        Assert.AreEqual(id, primary?.Id);
    }

    private static void RouteApplicationKeyboardFocus(UIElement target)
    {
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

    private static void RaiseKey(UIElement target, Key key)
    {
        PresentationSource inputSource = PresentationSource.FromVisual(target)
            ?? throw new InvalidOperationException(
                "The routed input target has no presentation source.");
        KeyEventArgs args = new(
            Keyboard.PrimaryDevice,
            inputSource,
            Environment.TickCount,
            key)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
            Source = target
        };
        target.RaiseEvent(args);
        Assert.IsTrue(args.Handled);
    }

    private static async Task WaitForScriptConditionAsync(
        CoreWebView2 core,
        string script)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (await ReadScriptBooleanAsync(core, script))
            {
                return;
            }
            await Task.Delay(25);
        }
        Assert.Fail($"Timed out waiting for trusted-page condition: {script}");
    }

    private static async Task<bool> ReadScriptBooleanAsync(
        CoreWebView2 core,
        string script)
    {
        string json = await core.ExecuteScriptAsync(script);
        return JsonSerializer.Deserialize<bool>(json);
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

    private static long GetRevision(MainWindow window) =>
        GetField<SourceRevisionTracker>(window, "_sourceRevisionTracker")
            .Current;

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

}
