using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MainWindowSourceSynchronizationIntegrationTests
{
    private const string TwoElementSource =
        "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"a\"/><circle id=\"b\"/></svg>";
    private const string RightClickSource =
        "<svg xmlns=\"http://www.w3.org/2000/svg\">\r\n  <rect id=\"a\"/>\r\n"
        + "  <circle id=\"b\"/>\r\n</svg>";

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void RealDocumentUndoRedoRestoresPrimaryAnchorAndInspector()
    {
        RunOnSta(window =>
        {
            InitializeDocument(window, TwoElementSource);
            SvgDocumentIndex document = GetDocument(window);
            SvgElementIdentity rectangle = Find(document, "a");
            SvgElementIdentity circle = Find(document, "b");
            ApplySelection(
                window,
                new SvgMultiSelectionState(
                    GetRevision(window),
                    [rectangle, circle],
                    rectangle,
                    rectangle));

            ApplyFillEdit(
                window,
                TwoElementSource,
                rectangle,
                preferredSelections: [rectangle, circle],
                preferredPrimary: circle);
            string edited = window.SourceEditor.Text;
            StringAssert.Contains(edited, "fill=\"blue\"");
            ApplyCurrentValidation(window, circle);
            AssertSelection(
                window,
                circle,
                circle,
                expectedCount: 2,
                expectedId: "b");

            InvokeUndo(window);
            Assert.AreEqual(TwoElementSource, window.SourceEditor.Text);
            ApplyCurrentValidation(window);
            AssertSelection(
                window,
                rectangle,
                rectangle,
                expectedCount: 2,
                expectedId: "a");
            Assert.IsFalse(window.SourceEditor.CanUndo);

            InvokeRedo(window);
            Assert.AreEqual(edited, window.SourceEditor.Text);
            ApplyCurrentValidation(window, rectangle);
            AssertSelection(
                window,
                circle,
                circle,
                expectedCount: 2,
                expectedId: "b");
        });
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void RealDocumentUndoRestoresIntentionallyEmptySelection()
    {
        RunOnSta(window =>
        {
            InitializeDocument(window, TwoElementSource);
            SvgDocumentIndex document = GetDocument(window);
            SvgElementIdentity rectangle = Find(document, "a");
            ApplySelection(
                window,
                SvgMultiSelectionState.Empty(GetRevision(window)));

            ApplyFillEdit(
                window,
                TwoElementSource,
                rectangle,
                preferredSelections: [rectangle],
                preferredPrimary: rectangle);
            ApplyCurrentValidation(window, rectangle);
            AssertSelection(
                window,
                rectangle,
                rectangle,
                expectedCount: 1,
                expectedId: "a");

            InvokeUndo(window);
            Assert.AreEqual(TwoElementSource, window.SourceEditor.Text);
            ApplyCurrentValidation(window);
            AssertEmptySelection(window);
        });
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void DeletingOldPrimaryInSourceKeepsSurvivingSelection()
    {
        RunOnSta(window =>
        {
            InitializeDocument(window, TwoElementSource);
            SvgDocumentIndex document = GetDocument(window);
            SvgElementNode rectangleNode = document.Elements.Single(element =>
                element.Id == "a");
            SvgElementIdentity rectangle = rectangleNode.Identity;
            SvgElementIdentity circle = Find(document, "b");
            ApplySelection(
                window,
                new SvgMultiSelectionState(
                    GetRevision(window),
                    [rectangle, circle],
                    rectangle,
                    rectangle));

            window.SourceEditor.Document.Remove(
                rectangleNode.FullSpan.Start,
                rectangleNode.FullSpan.Length);
            ApplyCurrentValidation(window);

            SvgMultiSelectionState selection =
                GetSelection(window);
            SvgElementIdentity survivingCircle =
                Find(GetDocument(window), "b");
            Assert.AreEqual(1, selection.Identities.Count);
            Assert.IsFalse(selection.Identities.Contains(rectangle));
            AssertSelection(
                window,
                survivingCircle,
                survivingCircle,
                expectedCount: 1,
                expectedId: "b");
            Assert.AreEqual(
                "circle",
                GetViewModel(window).Inspector.SelectedElement!.Element.Name);
        });
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void InterveningRevisionsInvalidateByteIdenticalPendingRestore()
    {
        RunOnSta(window =>
        {
            InitializeDocument(window, TwoElementSource);
            SvgDocumentIndex document = GetDocument(window);
            SvgElementIdentity rectangle = Find(document, "a");
            SvgElementIdentity circle = Find(document, "b");
            ApplySelection(
                window,
                new SvgMultiSelectionState(
                    GetRevision(window),
                    [rectangle, circle],
                    rectangle,
                    rectangle));

            ApplyFillEdit(
                window,
                TwoElementSource,
                rectangle,
                preferredSelections: [rectangle, circle],
                preferredPrimary: circle);
            ApplyCurrentValidation(window, circle);
            AssertSelection(
                window,
                circle,
                circle,
                expectedCount: 2,
                expectedId: "b");

            InvokeUndo(window);
            Assert.AreEqual(TwoElementSource, window.SourceEditor.Text);
            long undoRevision = GetRevision(window);
            Assert.IsNotNull(GetPendingSelectionRestore(window));

            int insertionOffset = TwoElementSource.IndexOf(
                "</svg>",
                StringComparison.Ordinal);
            window.SourceEditor.Document.Insert(insertionOffset, " ");
            window.SourceEditor.Document.Remove(insertionOffset, 1);
            Assert.AreEqual(TwoElementSource, window.SourceEditor.Text);
            Assert.AreEqual(undoRevision + 2, GetRevision(window));
            Assert.IsNull(GetPendingSelectionRestore(window));

            SvgDocumentIndex currentDocument =
                new SvgDocumentIndexService()
                    .Build(window.SourceEditor.Text).Document!;
            SvgElementIdentity currentCircle = Find(currentDocument, "b");
            ApplySelection(
                window,
                new SvgMultiSelectionState(
                    GetRevision(window),
                    [currentCircle],
                    currentCircle,
                    currentCircle),
                InspectorSelectionOrigin.PreviewNavigation);
            long revisionBeforeValidation = GetRevision(window);

            ApplyCurrentValidation(window);
            AssertSelection(
                window,
                currentCircle,
                currentCircle,
                expectedCount: 1,
                expectedId: "b");
            Assert.IsNull(GetPendingSelectionRestore(window));
            ApplyCurrentValidation(window);
            AssertSelection(
                window,
                currentCircle,
                currentCircle,
                expectedCount: 1,
                expectedId: "b");
            Assert.AreEqual(revisionBeforeValidation, GetRevision(window));
            Assert.AreEqual(TwoElementSource, window.SourceEditor.Text);
            Assert.IsTrue(window.SourceEditor.CanUndo);
        });
    }

    [TestMethod]
    [DataRow("Structure", DisplayName = "Structure selection")]
    [DataRow("Layers", DisplayName = "Layers selection")]
    [DataRow("Preview", DisplayName = "Preview selection")]
    [TestCategory("DesktopIntegration")]
    public void NewerExplicitSelectionSupersedesBoundUndoRestore(string route)
    {
        RunOnSta(window =>
        {
            InitializeDocument(window, TwoElementSource);
            SvgDocumentIndex document = GetDocument(window);
            SvgElementIdentity rectangle = Find(document, "a");
            SvgElementIdentity circle = Find(document, "b");
            ApplySelection(
                window,
                new SvgMultiSelectionState(
                    GetRevision(window),
                    [rectangle, circle],
                    rectangle,
                    rectangle));

            ApplyFillEdit(
                window,
                TwoElementSource,
                rectangle,
                preferredSelections: [rectangle, circle],
                preferredPrimary: rectangle);
            ApplyCurrentValidation(window, rectangle);
            InvokeUndo(window);

            Assert.AreEqual(TwoElementSource, window.SourceEditor.Text);
            Assert.IsNotNull(GetPendingSelectionRestore(window));
            bool modifiedBeforeNavigation = GetViewModel(window).IsModified;
            SelectThroughSelectionRoute(window, circle, route);
            SvgMultiSelectionState newerSelection = GetSelection(window);
            Assert.AreEqual(circle, newerSelection.Primary);

            Assert.IsNull(GetPendingSelectionRestore(window));
            ApplyCurrentValidation(window);
            AssertSelection(
                window,
                newerSelection.Primary!,
                newerSelection.Anchor!,
                newerSelection.Identities.Count,
                expectedId: "b");
            ApplyCurrentValidation(window);
            AssertSelection(
                window,
                newerSelection.Primary!,
                newerSelection.Anchor!,
                newerSelection.Identities.Count,
                expectedId: "b");
            Assert.AreEqual(TwoElementSource, window.SourceEditor.Text);
            Assert.IsFalse(window.SourceEditor.CanUndo);
            Assert.IsTrue(window.SourceEditor.CanRedo);
            Assert.AreEqual(
                modifiedBeforeNavigation,
                GetViewModel(window).IsModified);
        });
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void NewerFocusedSourceCaretSupersedesBoundUndoRestoreWithStaleIndex()
    {
        RunOnSta(window =>
        {
            window.Show();
            PumpUntil(
                () => window.IsLoaded,
                TimeSpan.FromSeconds(5),
                "The real MainWindow did not load.");
            InitializeDocument(window, TwoElementSource);
            SvgDocumentIndex document = GetDocument(window);
            SvgElementIdentity rectangle = Find(document, "a");
            SvgElementIdentity circle = Find(document, "b");
            ApplySelection(
                window,
                new SvgMultiSelectionState(
                    GetRevision(window),
                    [rectangle, circle],
                    rectangle,
                    rectangle));
            ApplyFillEdit(
                window,
                TwoElementSource,
                rectangle,
                preferredSelections: [rectangle, circle],
                preferredPrimary: rectangle);
            ApplyCurrentValidation(window, rectangle);
            InvokeUndo(window);

            Assert.IsNotNull(GetPendingSelectionRestore(window));
            Assert.IsFalse(GetField<bool>(window, "_isInspectorIndexCurrent"));
            window.Activate();
            window.SourceEditor.Focus();
            Keyboard.Focus(window.SourceEditor.TextArea);
            PumpUntil(
                () => window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "AvalonEdit did not acquire real keyboard focus.");

            SvgElementNode currentCircle =
                new SvgDocumentIndexService().Build(TwoElementSource).Document!
                    .Elements.Single(element => element.Id == "b");
            bool canUndoBeforeCaret = window.SourceEditor.CanUndo;
            bool modifiedBeforeCaret = GetViewModel(window).IsModified;
            window.SourceEditor.CaretOffset =
                currentCircle.StartTagSpan.Start + 1;

            Assert.IsNull(GetPendingSelectionRestore(window));
            Assert.IsNotNull(GetPendingSourceNavigationIntent(window));
            Assert.IsFalse(
                GetField<DispatcherTimer>(window, "_inspectorCaretTimer")
                    .IsEnabled,
                "A stale parsed index must not schedule immediate resolution.");

            ApplyCurrentValidation(window);
            PumpUntil(
                () => GetViewModel(window).Inspector.SelectedElement?.Element.Id
                    == "b",
                TimeSpan.FromSeconds(5),
                "Post-validation Source-caret synchronization did not win.");

            SvgElementIdentity currentCircleIdentity =
                GetViewModel(window).Inspector.SelectedElement!.Element.Identity;
            AssertSelection(
                window,
                currentCircleIdentity,
                rectangle,
                expectedCount: 2,
                expectedId: "b");
            Assert.IsNull(GetPendingSourceNavigationIntent(window));
            Assert.IsTrue(window.SourceEditor.IsKeyboardFocusWithin);
            Assert.AreEqual(TwoElementSource, window.SourceEditor.Text);
            Assert.AreEqual(canUndoBeforeCaret, window.SourceEditor.CanUndo);
            Assert.AreEqual(modifiedBeforeCaret, GetViewModel(window).IsModified);
        }, timeout: TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    [DataRow("Layers", DisplayName = "Layers right-click")]
    [DataRow("Structure", DisplayName = "Structure right-click")]
    [TestCategory("DesktopIntegration")]
    public void TreeRightClickSupersedesBoundUndoRestoreThroughActualHandler(
        string route)
    {
        RunOnSta(window =>
        {
            ShowWindow(window);
            (SvgElementIdentity rectangle, SvgElementIdentity circle) =
                PrepareBoundUndoRestore(window, afterPrimaryId: "a");
            MainViewModel viewModel = GetViewModel(window);
            TreeView tree = SelectInspectorTree(window, route);
            object target = route.Equals("Layers", StringComparison.Ordinal)
                ? viewModel.Inspector.FindLayerViewModel(
                    GetDocument(window).FindBestMatch(circle)!)!
                : viewModel.Inspector.FindViewModel(
                    GetDocument(window).FindBestMatch(circle)!)!;
            TreeViewItem item = FindTreeViewItem(tree, target);
            long intentGeneration = GetSelectionIntentGeneration(window);
            string exactSource = window.SourceEditor.Text;
            bool canUndo = window.SourceEditor.CanUndo;
            bool isModified = viewModel.IsModified;

            MouseButtonEventArgs args = new(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                MouseButton.Right)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent,
                Source = item
            };
            InvokePrivate(
                window,
                "OnInspectorTreePreviewMouseRightButtonDown",
                tree,
                args);
            PumpUntil(
                () => GetSelection(window).Primary == circle,
                TimeSpan.FromSeconds(5),
                $"{route} right-click did not select the requested row.");

            Assert.IsTrue(item.IsKeyboardFocusWithin);
            Assert.IsNull(GetPendingSelectionRestore(window));
            Assert.AreEqual(
                intentGeneration + 1,
                GetSelectionIntentGeneration(window));
            ApplyCurrentValidation(window);
            AssertSelection(
                window,
                circle,
                circle,
                expectedCount: 1,
                expectedId: "b");
            ApplyCurrentValidation(window);
            AssertSelection(
                window,
                circle,
                circle,
                expectedCount: 1,
                expectedId: "b");
            Assert.AreEqual(exactSource, window.SourceEditor.Text);
            Assert.AreEqual(canUndo, window.SourceEditor.CanUndo);
            Assert.AreEqual(isModified, viewModel.IsModified);
            Assert.AreEqual(rectangle, Find(GetDocument(window), "a"));
        }, timeout: TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    [DataRow("Layers", "Enter", DisplayName = "Layers Enter")]
    [DataRow("Layers", "Space", DisplayName = "Layers Space")]
    [DataRow("Structure", "Enter", DisplayName = "Structure Enter")]
    [DataRow("Structure", "Space", DisplayName = "Structure Space")]
    [TestCategory("DesktopIntegration")]
    public void TreeKeyboardActivationSupersedesBoundUndoRestoreThroughActualHandler(
        string route,
        string keyName)
    {
        RunOnSta(window =>
        {
            ShowWindow(window);
            (_, SvgElementIdentity circle) =
                PrepareBoundUndoRestore(window, afterPrimaryId: "b");
            MainViewModel viewModel = GetViewModel(window);
            TreeView tree = SelectInspectorTree(window, route);
            tree.Focus();
            Keyboard.Focus(tree);
            PumpUntil(
                () => tree.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                $"{route} did not acquire keyboard focus.");
            long intentGeneration = GetSelectionIntentGeneration(window);
            string exactSource = window.SourceEditor.Text;
            bool canUndo = window.SourceEditor.CanUndo;
            bool isModified = viewModel.IsModified;
            Key key = Enum.Parse<Key>(keyName);
            PresentationSource inputSource = PresentationSource.FromVisual(tree)
                ?? throw new InvalidOperationException(
                    "The inspector tree has no presentation source.");
            KeyEventArgs args = new(
                Keyboard.PrimaryDevice,
                inputSource,
                Environment.TickCount,
                key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
                Source = tree
            };

            InvokePrivate(
                window,
                route.Equals("Layers", StringComparison.Ordinal)
                    ? "OnLayersTreePreviewKeyDown"
                    : "OnInspectorTreePreviewKeyDown",
                tree,
                args);

            Assert.IsTrue(args.Handled);
            Assert.IsTrue(tree.IsKeyboardFocusWithin);
            Assert.IsNull(GetPendingSelectionRestore(window));
            Assert.AreEqual(
                intentGeneration + 1,
                GetSelectionIntentGeneration(window));
            ApplyCurrentValidation(window);
            AssertSelection(
                window,
                circle,
                circle,
                expectedCount: 1,
                expectedId: "b");
            ApplyCurrentValidation(window);
            AssertSelection(
                window,
                circle,
                circle,
                expectedCount: 1,
                expectedId: "b");
            Assert.AreEqual(exactSource, window.SourceEditor.Text);
            Assert.AreEqual(canUndo, window.SourceEditor.CanUndo);
            Assert.AreEqual(isModified, viewModel.IsModified);
        }, timeout: TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void FindNextFromFocusedFindBoxSupersedesBoundUndoRestore()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"a\"/><circle id=\"b\" data-name=\"needle\"/></svg>";
        RunOnSta(window =>
        {
            ShowWindow(window);
            InitializeDocument(window, source);
            SvgDocumentIndex document = GetDocument(window);
            SvgElementIdentity rectangle = Find(document, "a");
            ApplySelection(
                window,
                CreateSingleSelection(window, rectangle));
            ApplyFillEdit(
                window,
                source,
                rectangle,
                preferredSelections: [rectangle],
                preferredPrimary: rectangle);
            ApplyCurrentValidation(window, rectangle);
            InvokeUndo(window);
            Assert.IsNotNull(GetPendingSelectionRestore(window));
            Assert.IsFalse(GetField<bool>(window, "_isInspectorIndexCurrent"));

            InvokePrivate(
                window,
                "OnFindClick",
                window,
                new RoutedEventArgs());
            window.FindTextBox.Text = "needle";
            window.FindTextBox.Focus();
            Keyboard.Focus(window.FindTextBox);
            PumpUntil(
                () => window.FindTextBox.IsKeyboardFocused,
                TimeSpan.FromSeconds(5),
                "The Find box did not acquire real keyboard focus.");
            string exactSource = window.SourceEditor.Text;
            int expectedStart = exactSource.IndexOf(
                "needle",
                StringComparison.Ordinal);
            bool canUndo = window.SourceEditor.CanUndo;
            bool isModified = GetViewModel(window).IsModified;
            long intentGeneration = GetSelectionIntentGeneration(window);

            InvokePrivate(
                window,
                "OnFindNextClick",
                window,
                new RoutedEventArgs());
            PumpUntil(
                () => window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "Find Next did not transfer keyboard focus to Source.");

            Assert.AreEqual(expectedStart, window.SourceEditor.SelectionStart);
            Assert.AreEqual("needle", window.SourceEditor.SelectedText);
            Assert.IsNull(GetPendingSelectionRestore(window));
            Assert.IsNotNull(GetPendingSourceNavigationIntent(window));
            Assert.AreEqual(
                intentGeneration + 1,
                GetSelectionIntentGeneration(window));

            ApplyCurrentValidation(window);
            PumpUntil(
                () => GetViewModel(window).Inspector.SelectedElement?.Element.Id
                    == "b",
                TimeSpan.FromSeconds(5),
                "Find Next did not synchronize the containing circle.");
            SvgElementIdentity circle =
                GetViewModel(window).Inspector.SelectedElement!.Element.Identity;
            AssertSelection(
                window,
                circle,
                circle,
                expectedCount: 1,
                expectedId: "b");
            Assert.IsNull(GetPendingSelectionRestore(window));
            Assert.IsNull(GetPendingSourceNavigationIntent(window));
            Assert.AreEqual(expectedStart, window.SourceEditor.SelectionStart);
            Assert.AreEqual("needle", window.SourceEditor.SelectedText);
            Assert.AreEqual(exactSource, window.SourceEditor.Text);
            Assert.AreEqual(canUndo, window.SourceEditor.CanUndo);
            Assert.AreEqual(isModified, GetViewModel(window).IsModified);

            ApplyCurrentValidation(window);
            PumpFor(TimeSpan.FromMilliseconds(220));
            AssertSelection(
                window,
                circle,
                circle,
                expectedCount: 1,
                expectedId: "b");
            Assert.AreEqual(expectedStart, window.SourceEditor.SelectionStart);
            Assert.AreEqual("needle", window.SourceEditor.SelectedText);
        }, timeout: TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void SourceContextMenuSelectAllRegistersNavigationWhilePopupOwnsFocus()
    {
        RunOnSta(window =>
        {
            ShowWindow(window);
            PrepareBoundUndoRestore(window, afterPrimaryId: "a");
            ContextMenu menu = window.SourceEditor.ContextMenu;
            MenuItem selectAll = menu.Items.OfType<MenuItem>().Single(item =>
                Equals(item.Tag, SourceEditorContextCommand.SelectAll));
            window.FindTextBox.Focus();
            Keyboard.Focus(window.FindTextBox);
            PumpUntil(
                () => !window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "The Source editor unexpectedly retained keyboard focus.");
            long intentGeneration = GetSelectionIntentGeneration(window);
            string exactSource = window.SourceEditor.Text;
            bool canUndo = window.SourceEditor.CanUndo;
            bool isModified = GetViewModel(window).IsModified;

            selectAll.RaiseEvent(
                new RoutedEventArgs(MenuItem.ClickEvent, selectAll));

            Assert.AreEqual(0, window.SourceEditor.SelectionStart);
            Assert.AreEqual(exactSource.Length, window.SourceEditor.SelectionLength);
            Assert.IsNull(GetPendingSelectionRestore(window));
            Assert.IsNotNull(GetPendingSourceNavigationIntent(window));
            Assert.AreEqual(
                intentGeneration + 1,
                GetSelectionIntentGeneration(window));
            Assert.AreEqual(exactSource, window.SourceEditor.Text);
            Assert.AreEqual(canUndo, window.SourceEditor.CanUndo);
            Assert.AreEqual(isModified, GetViewModel(window).IsModified);
        }, timeout: TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void SourceRightClickSupersedesBoundUndoRestoreThroughActualHandler()
    {
        RunOnSta(window =>
        {
            ShowWindow(window);
            PrepareBoundUndoRestore(
                window,
                afterPrimaryId: "a",
                source: RightClickSource);
            SvgDocumentIndex currentSourceDocument =
                new SvgDocumentIndexService().Build(RightClickSource).Document!;
            SvgElementNode rectangle = currentSourceDocument.Elements.Single(
                element => element.Id == "a");
            SvgElementNode circle = currentSourceDocument.Elements.Single(
                element => element.Id == "b");
            MainViewModel viewModel = GetViewModel(window);
            window.LayersTree.Focus();
            Keyboard.Focus(window.LayersTree);
            PumpUntil(
                () => window.LayersTree.IsKeyboardFocusWithin
                    && !window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "Layers did not acquire real keyboard focus away from Source.");
            window.SourceEditor.Select(rectangle.StartTagSpan.Start + 1, 0);
            long intentGeneration = GetSelectionIntentGeneration(window);
            string exactSource = window.SourceEditor.Text;
            bool canUndo = window.SourceEditor.CanUndo;
            bool canRedo = window.SourceEditor.CanRedo;
            bool isModified = viewModel.IsModified;

            MouseButtonEventArgs args = InvokeSourceRightClickAtOffset(
                window,
                circle.StartTagSpan.Start + 2);
            int rightClickedOffset = window.SourceEditor.CaretOffset;

            Assert.IsFalse(args.Handled);
            Assert.IsTrue(window.LayersTree.IsKeyboardFocusWithin);
            Assert.IsFalse(window.SourceEditor.IsKeyboardFocusWithin);
            Assert.AreEqual(0, window.SourceEditor.SelectionLength);
            Assert.IsTrue(
                circle.StartTagSpan.Contains(rightClickedOffset),
                "The real Source right-click handler did not place the caret in the circle.");
            Assert.IsNull(GetPendingSelectionRestore(window));
            Assert.IsNotNull(GetPendingSourceNavigationIntent(window));
            Assert.AreEqual(
                intentGeneration + 1,
                GetSelectionIntentGeneration(window));

            ContextMenu menu = window.SourceEditor.ContextMenu;
            RaiseContextMenuClosed(menu);
            window.SourceEditor.Focus();
            Keyboard.Focus(window.SourceEditor.TextArea);
            PumpUntil(
                () => window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "The simulated native popup close did not restore Source focus.");
            PumpFor(TimeSpan.FromMilliseconds(80));
            Assert.IsNotNull(GetPendingSourceNavigationIntent(window));

            ApplyCurrentValidation(window);
            PumpUntil(
                () => viewModel.Inspector.SelectedElement?.Element.Id == "b",
                TimeSpan.FromSeconds(5),
                "Validation did not synchronize Inspector to the right-clicked circle.");
            SvgElementIdentity currentCircle =
                viewModel.Inspector.SelectedElement!.Element.Identity;
            AssertSelection(
                window,
                currentCircle,
                currentCircle,
                expectedCount: 1,
                expectedId: "b");
            Assert.AreEqual(rightClickedOffset, window.SourceEditor.CaretOffset);
            Assert.IsNull(GetPendingSelectionRestore(window));
            Assert.IsNull(GetPendingSourceNavigationIntent(window));

            ApplyCurrentValidation(window);
            PumpFor(TimeSpan.FromMilliseconds(220));
            AssertSelection(
                window,
                currentCircle,
                currentCircle,
                expectedCount: 1,
                expectedId: "b");
            Assert.AreEqual(rightClickedOffset, window.SourceEditor.CaretOffset);
            Assert.AreEqual(exactSource, window.SourceEditor.Text);
            Assert.AreEqual(canUndo, window.SourceEditor.CanUndo);
            Assert.AreEqual(canRedo, window.SourceEditor.CanRedo);
            Assert.AreEqual(isModified, viewModel.IsModified);
            Assert.IsTrue(window.SourceEditor.IsKeyboardFocusWithin);
        }, timeout: TimeSpan.FromSeconds(30));
    }
    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void DeletingAllSelectedElementsInSourceSelectsCurrentSvgRoot()
    {
        const string emptyRoot =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" id=\"root\" width=\"100\" height=\"100\"></svg>";
        RunOnSta(window =>
        {
            InitializeDocument(window, TwoElementSource);
            SvgDocumentIndex document = GetDocument(window);
            SvgElementIdentity rectangle = Find(document, "a");
            SvgElementIdentity circle = Find(document, "b");
            ApplySelection(
                window,
                new SvgMultiSelectionState(
                    GetRevision(window),
                    [rectangle, circle],
                    rectangle,
                    rectangle));

            window.SourceEditor.Text = emptyRoot;
            ApplyCurrentValidation(window);

            MainViewModel viewModel = GetViewModel(window);
            Assert.AreEqual(
                "svg",
                viewModel.Inspector.SelectedElement?.Element.Name);
            Assert.IsTrue(viewModel.Inspector.Properties.Any(property =>
                property.Name == "id" && property.Value == "root"));
            SvgElementIdentity root =
                viewModel.Inspector.SelectedElement!.Element.Identity;
            SvgMultiSelectionState rootSelection = GetSelection(window);
            Assert.AreEqual(root, rootSelection.Primary);
            Assert.AreEqual(root, rootSelection.Anchor);
            Assert.AreEqual(1, rootSelection.Identities.Count);
            Assert.IsNull(
                viewModel.Inspector.SelectedLayer,
                "The SVG root is a Structure/Properties selection, not a layer row.");
            Assert.AreEqual(emptyRoot, window.SourceEditor.Text);
        });
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void RealSourceContextMenuClosedHandlerRequeuesCancelledCaretRequest()
    {
        RunOnSta(window =>
        {
            window.Show();
            PumpUntil(
                () => window.IsLoaded,
                TimeSpan.FromSeconds(5),
                "The real MainWindow did not load.");
            InitializeDocument(window, TwoElementSource);
            SvgDocumentIndex document = GetDocument(window);
            SvgElementNode rectangle = document.Elements.Single(element =>
                element.Id == "a");
            SvgElementIdentity circle = Find(document, "b");
            ApplySelection(
                window,
                new SvgMultiSelectionState(
                    GetRevision(window),
                    [circle],
                    circle,
                    circle));

            window.Activate();
            window.SourceEditor.Focus();
            Keyboard.Focus(window.SourceEditor.TextArea);
            PumpUntil(
                () => window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "AvalonEdit did not acquire real keyboard focus.");
            window.SourceEditor.CaretOffset =
                rectangle.StartTagSpan.Start + 1;
            DispatcherTimer caretTimer =
                GetField<DispatcherTimer>(window, "_inspectorCaretTimer");
            Assert.IsTrue(caretTimer.IsEnabled);
            long oldGeneration = GetField<long>(
                window,
                "_inspectorCaretRequestGeneration");
            string exactSource = window.SourceEditor.Text;
            bool canUndo = window.SourceEditor.CanUndo;
            bool isModified = GetViewModel(window).IsModified;

            window.LayersTree.Focus();
            Keyboard.Focus(window.LayersTree);
            PumpUntil(
                () => window.LayersTree.IsKeyboardFocusWithin
                    && !window.SourceEditor.IsKeyboardFocusWithin
                    && !caretTimer.IsEnabled,
                TimeSpan.FromSeconds(5),
                "Real Source focus loss did not cancel the old request.");
            Assert.IsNull(GetField<long?>(
                window,
                "_scheduledInspectorCaretRequestGeneration"));

            ContextMenu menu = window.SourceEditor.ContextMenu;
            // Headless WPF does not reliably complete the native popup focus
            // transfer. Raise the real Closed event, then model the platform's
            // Source-focus return before the queued production callback runs.
            RaiseContextMenuClosed(menu);
            window.SourceEditor.Focus();
            Keyboard.Focus(window.SourceEditor.TextArea);
            PumpUntil(
                () => window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "The simulated popup close did not restore Source focus.");
            PumpUntil(
                () => GetViewModel(window).Inspector.SelectedElement?.Element.Id
                    == "a",
                TimeSpan.FromSeconds(5),
                "The fresh post-menu caret request did not update Inspector.");

            Assert.IsTrue(
                GetField<long>(window, "_inspectorCaretRequestGeneration")
                    > oldGeneration);
            Assert.IsTrue(window.SourceEditor.IsKeyboardFocusWithin);
            Assert.AreEqual(exactSource, window.SourceEditor.Text);
            Assert.AreEqual(canUndo, window.SourceEditor.CanUndo);
            Assert.AreEqual(isModified, GetViewModel(window).IsModified);
            Assert.IsNull(GetPendingSourceNavigationIntent(window));
        }, timeout: TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void SourceContextMenuClosedFailsClosedForUnfocusedStaleInvalidAndClosingStates()
    {
        RunOnSta(window =>
        {
            window.Show();
            PumpUntil(
                () => window.IsLoaded,
                TimeSpan.FromSeconds(5),
                "The real MainWindow did not load.");
            InitializeDocument(window, TwoElementSource);
            SvgElementNode rectangle = GetDocument(window).Elements.Single(
                element => element.Id == "a");
            DispatcherTimer caretTimer =
                GetField<DispatcherTimer>(window, "_inspectorCaretTimer");
            ContextMenu menu = window.SourceEditor.ContextMenu;
            menu.PlacementTarget = window.SourceEditor;

            window.Activate();
            window.SourceEditor.Focus();
            Keyboard.Focus(window.SourceEditor.TextArea);
            PumpUntil(
                () => window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "AvalonEdit did not acquire real keyboard focus.");
            window.SourceEditor.CaretOffset = rectangle.StartTagSpan.Start + 1;
            Assert.IsTrue(caretTimer.IsEnabled);
            window.LayersTree.Focus();
            Keyboard.Focus(window.LayersTree);
            PumpUntil(
                () => window.LayersTree.IsKeyboardFocusWithin
                    && !caretTimer.IsEnabled,
                TimeSpan.FromSeconds(5),
                "Moving focus to Layers did not cancel the request.");
            RaiseContextMenuClosed(menu);
            window.LayersTree.Focus();
            Keyboard.Focus(window.LayersTree);
            PumpFor(TimeSpan.FromMilliseconds(220));
            Assert.IsFalse(caretTimer.IsEnabled);

            window.SourceEditor.Focus();
            Keyboard.Focus(window.SourceEditor.TextArea);
            PumpUntil(
                () => window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "Source focus was not restored for the stale-index case.");
            window.SourceEditor.CaretOffset = rectangle.StartTagSpan.Start + 2;
            Assert.IsTrue(caretTimer.IsEnabled);
            menu.IsOpen = true;
            PumpFor(TimeSpan.FromMilliseconds(40));
            int insertionOffset = window.SourceEditor.Text.IndexOf(
                "</svg>",
                StringComparison.Ordinal);
            window.SourceEditor.Document.Insert(insertionOffset, " ");
            Assert.IsFalse(GetField<bool>(window, "_isInspectorIndexCurrent"));
            menu.IsOpen = false;
            PumpFor(TimeSpan.FromMilliseconds(220));
            Assert.IsFalse(caretTimer.IsEnabled);

            window.SourceEditor.Text = "<svg";
            ApplyCurrentValidation(window);
            Assert.IsNull(GetViewModel(window).Inspector.DocumentIndex);
            OpenAndClose(menu);
            PumpFor(TimeSpan.FromMilliseconds(220));
            Assert.IsFalse(caretTimer.IsEnabled);

            InitializeDocument(window, TwoElementSource);
            InvokePrivate(window, "CancelPendingInspectorCaretSynchronization");
            SetField(window, "_isWindowClosing", true);
            try
            {
                OpenAndClose(menu);
                PumpFor(TimeSpan.FromMilliseconds(220));
                Assert.IsFalse(caretTimer.IsEnabled);
            }
            finally
            {
                SetField(window, "_isWindowClosing", false);
            }
        }, timeout: TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void RealDelayedCaretRequestCannotOverrideNewerFocusedTreeSelection()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"a\"/><circle id=\"b\"/><text id=\"c\">text</text></svg>";
        RunOnSta(window =>
        {
            window.Show();
            PumpUntil(
                () => GetField<bool>(window, "_isWebViewReady"),
                TimeSpan.FromSeconds(20),
                "WebView2 did not become ready for the real-window focus test.");
            InitializeDocument(window, source);

            SvgDocumentIndex document = GetDocument(window);
            SvgElementNode rectangleNode = document.Elements.Single(element =>
                element.Id == "a");
            SvgElementIdentity circle = Find(document, "b");
            SvgElementIdentity text = Find(document, "c");

            window.Activate();
            window.SourceEditor.Focus();
            Keyboard.Focus(window.SourceEditor.TextArea);
            PumpUntil(
                () => window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "AvalonEdit did not acquire real keyboard focus.");

            InvokePrivate(window, "CancelPendingInspectorCaretSynchronization");
            window.SourceEditor.CaretOffset =
                rectangleNode.StartTagSpan.Start + 1;
            DispatcherTimer caretTimer =
                GetField<DispatcherTimer>(window, "_inspectorCaretTimer");
            Assert.IsTrue(
                caretTimer.IsEnabled,
                "The real caret event did not schedule the production timer.");

            string exactSource = window.SourceEditor.Text;
            bool canUndo = window.SourceEditor.CanUndo;
            bool isModified = GetViewModel(window).IsModified;

            window.LayersTree.Focus();
            Keyboard.Focus(window.LayersTree);
            PumpUntil(
                () => window.LayersTree.IsKeyboardFocusWithin
                    && !window.SourceEditor.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(5),
                "Keyboard focus did not move from Source to Layers.");

            ApplySelection(
                window,
                new SvgMultiSelectionState(
                    GetRevision(window),
                    [circle, text],
                    text,
                    circle),
                InspectorSelectionOrigin.PreviewNavigation);
            PumpFor(TimeSpan.FromMilliseconds(320));

            AssertSelection(
                window,
                text,
                circle,
                expectedCount: 2,
                expectedId: "c");
            Assert.AreNotEqual(
                rectangleNode.Identity,
                GetSelection(window).Primary);
            Assert.AreEqual(exactSource, window.SourceEditor.Text);
            Assert.AreEqual(canUndo, window.SourceEditor.CanUndo);
            Assert.AreEqual(isModified, GetViewModel(window).IsModified);
            Assert.IsFalse(window.SourceEditor.IsKeyboardFocusWithin);
            Assert.IsTrue(window.LayersTree.IsKeyboardFocusWithin);
            Assert.IsFalse(caretTimer.IsEnabled);
            Assert.IsNull(GetField<long?>(
                window,
                "_scheduledInspectorCaretRequestGeneration"));
        }, timeout: TimeSpan.FromSeconds(45));
    }

    private static void ShowWindow(MainWindow window)
    {
        window.Show();
        PumpUntil(
            () => window.IsLoaded,
            TimeSpan.FromSeconds(5),
            "The real MainWindow did not load.");
        window.Activate();
    }

    private static (SvgElementIdentity Rectangle, SvgElementIdentity Circle)
        PrepareBoundUndoRestore(
            MainWindow window,
            string afterPrimaryId,
            string source = TwoElementSource)
    {
        InitializeDocument(window, source);
        SvgDocumentIndex document = GetDocument(window);
        SvgElementIdentity rectangle = Find(document, "a");
        SvgElementIdentity circle = Find(document, "b");
        SvgElementIdentity afterPrimary = afterPrimaryId == "a"
            ? rectangle
            : circle;
        ApplySelection(window, CreateSingleSelection(window, rectangle));
        ApplyFillEdit(
            window,
            source,
            rectangle,
            preferredSelections: [afterPrimary],
            preferredPrimary: afterPrimary);
        ApplyCurrentValidation(window, afterPrimary);
        AssertSelection(
            window,
            afterPrimary,
            afterPrimary,
            expectedCount: 1,
            expectedId: afterPrimaryId);
        InvokeUndo(window);
        Assert.AreEqual(source, window.SourceEditor.Text);
        Assert.IsNotNull(GetPendingSelectionRestore(window));
        Assert.IsFalse(GetField<bool>(window, "_isInspectorIndexCurrent"));
        return (rectangle, circle);
    }

    private static SvgMultiSelectionState CreateSingleSelection(
        MainWindow window,
        SvgElementIdentity identity) =>
        new(
            GetRevision(window),
            [identity],
            identity,
            identity);

    private static TreeView SelectInspectorTree(
        MainWindow window,
        string route)
    {
        bool layers = route.Equals("Layers", StringComparison.Ordinal);
        window.InspectorModeTabs.SelectedItem = layers
            ? window.LayersTab
            : window.StructureTab;
        PumpFor(TimeSpan.FromMilliseconds(80));
        TreeView tree = layers ? window.LayersTree : window.InspectorTree;
        tree.UpdateLayout();
        return tree;
    }

    private static TreeViewItem FindTreeViewItem(
        ItemsControl parent,
        object target)
    {
        parent.UpdateLayout();
        foreach (object item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item)
                is not TreeViewItem container)
            {
                continue;
            }
            if (ReferenceEquals(item, target))
            {
                return container;
            }

            container.IsExpanded = true;
            container.UpdateLayout();
            try
            {
                return FindTreeViewItem(container, target);
            }
            catch (InvalidOperationException)
            {
                // Continue through the remaining realized branches.
            }
        }

        throw new InvalidOperationException(
            "The requested Inspector item container was not realized.");
    }

    private static void InitializeDocument(
        MainWindow window,
        string source)
    {
        window.SourceEditor.Text = source;
        GetField<AsyncDebouncer>(window, "_previewDebouncer").Cancel();
        window.SourceEditor.Document.UndoStack.ClearAll();
        window.SourceEditor.Document.UndoStack.MarkAsOriginalFile();
        ApplyCurrentValidation(window);
        GetViewModel(window).MarkSaved(string.Empty);
    }

    private static void ApplyFillEdit(
        MainWindow window,
        string source,
        SvgElementIdentity rectangle,
        IReadOnlyList<SvgElementIdentity> preferredSelections,
        SvgElementIdentity preferredPrimary)
    {
        SvgElementNode node =
            new SvgDocumentIndexService().Build(source).Document!
                .FindBestMatch(rectangle)!;
        SvgAttributeEditResult edit =
            new SvgAttributeEditService().CreateEdit(
                source,
                node,
                "fill",
                "blue");
        InvokePrivate(
            window,
            "ApplyDocumentEditWithSelection",
            edit.Edit!,
            source,
            preferredSelections,
            preferredPrimary,
            false,
            SourceChangeOrigin.VisualCommand);
    }

    private static void ApplySelection(
        MainWindow window,
        SvgMultiSelectionState selection,
        InspectorSelectionOrigin origin =
            InspectorSelectionOrigin.InspectorRestore)
    {
        InvokePrivate(
            window,
            "ApplyVisualSelectionState",
            selection,
            origin,
            false);
    }

    private static void SelectThroughSelectionRoute(
        MainWindow window,
        SvgElementIdentity identity,
        string route)
    {
        if (route.Equals("Preview", StringComparison.Ordinal))
        {
            ApplySelection(
                window,
                new SvgMultiSelectionState(
                    GetRevision(window),
                    [identity],
                    identity,
                    identity),
                InspectorSelectionOrigin.PreviewNavigation);
            return;
        }

        MainViewModel viewModel = GetViewModel(window);
        SvgElementNode node = viewModel.Inspector.DocumentIndex!
            .FindBestMatch(identity)!;
        if (route.Equals("Layers", StringComparison.Ordinal))
        {
            SvgLayerViewModel layer =
                viewModel.Inspector.FindLayerViewModel(node)!;
            layer.SetSelected(
                true,
                InspectorSelectionOrigin.ExplicitTreeNavigation);
            InvokePrivate(
                window,
                "OnLayersTreeSelectionChanged",
                window.LayersTree,
                new RoutedPropertyChangedEventArgs<object>(
                    viewModel.Inspector.SelectedLayer ?? layer,
                    layer));
            return;
        }

        Assert.AreEqual("Structure", route);
        SvgElementViewModel element =
            viewModel.Inspector.FindViewModel(node)!;
        element.SetSelected(
            true,
            InspectorSelectionOrigin.ExplicitTreeNavigation);
        InvokePrivate(
            window,
            "OnInspectorTreeSelectionChanged",
            window.InspectorTree,
            new RoutedPropertyChangedEventArgs<object>(
                viewModel.Inspector.SelectedElement ?? element,
                element));
    }

    private static MouseButtonEventArgs InvokeSourceRightClickAtOffset(
        MainWindow window,
        int offset)
    {
        TextView textView = window.SourceEditor.TextArea.TextView;
        window.SourceEditor.ScrollToLine(
            window.SourceEditor.Document.GetLineByOffset(offset).LineNumber);
        double originalLeft = window.Left;
        double originalTop = window.Top;
        Assert.IsFalse(
            double.IsNaN(originalLeft) || double.IsNaN(originalTop),
            "The shown test window must have a stable screen position.");
        try
        {
            Point documentPoint = default;
            Point targetScreen = default;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                window.UpdateLayout();
                textView.EnsureVisualLines();
                var location = window.SourceEditor.Document.GetLocation(offset);
                TextViewPosition textPosition = new(
                    location.Line,
                    location.Column);
                documentPoint = textView.GetVisualPosition(
                    textPosition,
                    VisualYPosition.LineMiddle);
                targetScreen = textView.PointToScreen(
                    documentPoint - textView.ScrollOffset);
                Point pointerScreen = textView.PointToScreen(
                    Mouse.GetPosition(textView));
                Vector delta = pointerScreen - targetScreen;
                if (Math.Abs(delta.X) <= 0.75
                    && Math.Abs(delta.Y) <= 0.75)
                {
                    break;
                }

                DpiScale dpi = VisualTreeHelper.GetDpi(window);
                window.Left += delta.X / dpi.DpiScaleX;
                window.Top += delta.Y / dpi.DpiScaleY;
                PumpFor(TimeSpan.FromMilliseconds(40));
                Mouse.Synchronize();
            }

            Point actualDocumentPoint =
                Mouse.GetPosition(textView) + textView.ScrollOffset;
            TextViewPosition? actualTextPosition =
                textView.GetPositionFloor(actualDocumentPoint);
            Assert.IsNotNull(
                actualTextPosition,
                $"The simulated pointer did not map into Source. Target screen={targetScreen}; actual document={actualDocumentPoint}.");
            int actualOffset = window.SourceEditor.Document.GetOffset(
                actualTextPosition.Value.Location);
            Assert.AreEqual(
                offset,
                actualOffset,
                $"The simulated pointer mapped to offset {actualOffset}, not {offset}. Target screen={targetScreen}; actual document={actualDocumentPoint}; requested document={documentPoint}.");
            MouseButtonEventArgs args = new(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                MouseButton.Right)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent,
                Source = textView
            };
            InvokePrivate(
                window,
                "OnSourceEditorPreviewMouseRightButtonDown",
                window.SourceEditor,
                args);
            return args;
        }
        finally
        {
            window.Left = originalLeft;
            window.Top = originalTop;
            window.UpdateLayout();
            Mouse.Synchronize();
        }
    }
    private static void RaiseContextMenuClosed(ContextMenu menu) =>
        menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent, menu));

    private static void OpenAndClose(ContextMenu menu)
    {
        menu.IsOpen = true;
        PumpFor(TimeSpan.FromMilliseconds(40));
        Assert.IsTrue(menu.IsOpen);
        menu.IsOpen = false;
    }

    private static void InvokeUndo(MainWindow window) =>
        InvokePrivate(
            window,
            "OnUndoClick",
            window,
            new RoutedEventArgs());

    private static void InvokeRedo(MainWindow window) =>
        InvokePrivate(
            window,
            "OnRedoClick",
            window,
            new RoutedEventArgs());

    private static void ApplyCurrentValidation(
        MainWindow window,
        SvgElementIdentity? preferredSelection = null)
    {
        string source = window.SourceEditor.Text;
        InvokePrivate(
            window,
            "ApplyValidationResult",
            source,
            GetRevision(window),
            new SvgDocumentIndexService().Build(source),
            preferredSelection,
            false);
    }

    private static SvgDocumentIndex GetDocument(MainWindow window) =>
        GetViewModel(window).Inspector.DocumentIndex!;

    private static SvgElementIdentity Find(
        SvgDocumentIndex document,
        string id) =>
        document.Elements.Single(element => element.Id == id).Identity;

    private static SvgMultiSelectionState GetSelection(MainWindow window) =>
        GetField<SvgMultiSelectionState>(window, "_visualSelectionState");

    private static object? GetPendingSelectionRestore(MainWindow window) =>
        GetField<object?>(window, "_pendingSelectionRestore");

    private static object? GetPendingSourceNavigationIntent(MainWindow window) =>
        GetField<object?>(window, "_pendingSourceNavigationIntent");

    private static MainViewModel GetViewModel(MainWindow window) =>
        GetField<MainViewModel>(window, "_viewModel");

    private static void AssertSelection(
        MainWindow window,
        SvgElementIdentity expectedPrimary,
        SvgElementIdentity expectedAnchor,
        int expectedCount,
        string expectedId)
    {
        SvgMultiSelectionState selection = GetSelection(window);
        MainViewModel viewModel = GetViewModel(window);

        Assert.AreEqual(expectedPrimary, selection.Primary);
        Assert.AreEqual(expectedAnchor, selection.Anchor);
        Assert.AreEqual(expectedCount, selection.Identities.Count);
        Assert.IsTrue(selection.Identities.Contains(expectedPrimary));
        Assert.IsTrue(selection.Identities.Contains(expectedAnchor));
        Assert.AreEqual(
            expectedPrimary,
            viewModel.Inspector.SelectedElement!.Element.Identity);
        Assert.IsNotNull(viewModel.Inspector.SelectedLayer);
        Assert.IsTrue(viewModel.Inspector.Properties.Any(property =>
            property.Name == "id" && property.Value == expectedId));
    }

    private static void AssertEmptySelection(MainWindow window)
    {
        SvgMultiSelectionState selection = GetSelection(window);
        MainViewModel viewModel = GetViewModel(window);

        Assert.AreEqual(0, selection.Identities.Count);
        Assert.IsNull(selection.Primary);
        Assert.IsNull(selection.Anchor);
        Assert.IsNull(viewModel.Inspector.SelectedElement);
        Assert.IsNull(viewModel.Inspector.SelectedLayer);
        Assert.AreEqual(0, viewModel.Inspector.Properties.Count);
    }

    private static long GetRevision(MainWindow window) =>
        GetField<SourceRevisionTracker>(window, "_sourceRevisionTracker").Current;

    private static long GetSelectionIntentGeneration(MainWindow window) =>
        GetField<long>(window, "_selectionIntentGeneration");

    private static void PumpFor(TimeSpan duration)
    {
        DispatcherFrame frame = new();
        DispatcherTimer timer = new(
            duration,
            DispatcherPriority.ApplicationIdle,
            (_, _) => frame.Continue = false,
            Dispatcher.CurrentDispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    private static void PumpUntil(
        Func<bool> condition,
        TimeSpan timeout,
        string failureMessage)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < timeout)
        {
            DispatcherFrame frame = new();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        Assert.IsTrue(condition(), failureMessage);
    }

    private static void RunOnSta(
        Action<MainWindow> test,
        TimeSpan? timeout = null)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            string localApplicationData = Path.Combine(
                Path.GetTempPath(),
                "SvgLiveEditor.SourceSynchronization.Tests",
                Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                window = new MainWindow(localApplicationData);
                test(window);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                if (window is not null)
                {
                    GetViewModel(window).MarkSaved(string.Empty);
                    window.Close();
                }
                TryDeleteTestDirectory(localApplicationData);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.IsTrue(
            thread.Join(timeout ?? TimeSpan.FromSeconds(30)),
            "The source synchronization STA did not exit.");
        if (failure is not null)
        {
            Assert.Fail(
                $"The real MainWindow source synchronization route failed.{Environment.NewLine}{failure}");
        }
    }

    private static void TryDeleteTestDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // This random test-owned directory is safe for normal OS cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // WebView2 can briefly retain a handle after the test window closes.
        }
    }

    private static void InvokePrivate(
        object instance,
        string methodName,
        params object?[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().Name, methodName);
        try
        {
            method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    private static void SetField<T>(
        object instance,
        string fieldName,
        T value)
    {
        FieldInfo field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().Name, fieldName);
        field.SetValue(instance, value);
    }

    private static T GetField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().Name, fieldName);
        return (T)field.GetValue(instance)!;
    }

}
