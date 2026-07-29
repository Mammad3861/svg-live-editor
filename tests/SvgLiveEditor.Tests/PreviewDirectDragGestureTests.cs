using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewDirectDragGestureTests
{
    private const string GestureId = "00112233445566778899AABBCCDDEEFF";
    private readonly PreviewPointerGestureArbiter _arbiter = new();

    [TestMethod]
    public void AltPrimaryMouseDragOverArtwork_SelectsOutboundDrag()
    {
        Assert.AreEqual(
            PreviewPointerGestureAction.OutboundDrag,
            _arbiter.Resolve(CreateGesture()));
    }

    [TestMethod]
    public void PlainLeftDragOutsideArtwork_DoesNothing()
    {
        Assert.AreEqual(
            PreviewPointerGestureAction.None,
            _arbiter.Resolve(CreateGesture() with
            {
                StartedOnArtwork = false
            }));
    }

    [TestMethod]
    public void PanModeAndAlternatePanGesturesTakePriority()
    {
        Assert.AreEqual(
            PreviewPointerGestureAction.Pan,
            _arbiter.Resolve(CreateGesture() with
            {
                PanModeEnabled = true
            }));
        Assert.AreEqual(
            PreviewPointerGestureAction.Pan,
            _arbiter.Resolve(CreateGesture() with
            {
                ControlHeld = true
            }));
        Assert.AreEqual(
            PreviewPointerGestureAction.Pan,
            _arbiter.Resolve(CreateGesture() with
            {
                SpaceHeld = true
            }));
        Assert.AreEqual(
            PreviewPointerGestureAction.Pan,
            _arbiter.Resolve(CreateGesture() with
            {
                Button = 1
            }));
    }

    [TestMethod]
    public void NonPrimaryNonMouseAndConflictingModifiedDrags_DoNotExport()
    {
        Assert.AreEqual(
            PreviewPointerGestureAction.None,
            _arbiter.Resolve(CreateGesture() with { IsPrimary = false }));
        Assert.AreEqual(
            PreviewPointerGestureAction.None,
            _arbiter.Resolve(CreateGesture() with { IsMouse = false }));
        Assert.AreEqual(
            PreviewPointerGestureAction.None,
            _arbiter.Resolve(CreateGesture() with { ShiftHeld = true }));
        Assert.AreEqual(
            PreviewPointerGestureAction.None,
            _arbiter.Resolve(CreateGesture() with { AltHeld = false }));
        Assert.AreEqual(
            PreviewPointerGestureAction.None,
            _arbiter.Resolve(CreateGesture() with { MetaHeld = true }));
    }

    [TestMethod]
    public void ArmedGesture_CrossesSystemThresholdAndStartsExactlyOnce()
    {
        PreviewDirectDragHandshake handshake = new();
        Assert.IsTrue(handshake.TryArm(
            CreateArmRequest(),
            isLeftButtonPressed: true));

        PreviewDirectDragSignal start = CreateSignal(
            PreviewDirectDragSignalAction.Start,
            x: 14,
            y: 10);
        Assert.IsTrue(handshake.TryStart(
            start,
            isLeftButtonPressed: true,
            isPanModeEnabled: false,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
        Assert.IsFalse(handshake.TryStart(
            start,
            isLeftButtonPressed: true,
            isPanModeEnabled: false,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
    }

    [TestMethod]
    public void MovementBelowSystemThreshold_DoesNotStart()
    {
        PreviewDirectDragHandshake handshake = new();
        handshake.TryArm(CreateArmRequest(), isLeftButtonPressed: true);

        Assert.IsFalse(handshake.TryStart(
            CreateSignal(
                PreviewDirectDragSignalAction.Start,
                x: 13,
                y: 13),
            isLeftButtonPressed: true,
            isPanModeEnabled: false,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
        Assert.IsTrue(handshake.IsArmed);
    }

    [TestMethod]
    public void StartWithoutGenuineArmOrPressedButton_IsRejected()
    {
        PreviewDirectDragHandshake handshake = new();
        PreviewDirectDragSignal start = CreateSignal(
            PreviewDirectDragSignalAction.Start,
            x: 20,
            y: 20);

        Assert.IsFalse(handshake.TryStart(
            start,
            isLeftButtonPressed: true,
            isPanModeEnabled: false,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
        Assert.IsFalse(handshake.TryArm(
            CreateArmRequest(),
            isLeftButtonPressed: false));
        Assert.IsFalse(handshake.TryStart(
            start,
            isLeftButtonPressed: true,
            isPanModeEnabled: false,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
    }

    [TestMethod]
    public void PanTransitionReleaseCancelAndFocusLoss_ResetGesture()
    {
        PreviewDirectDragHandshake handshake = new();
        handshake.TryArm(CreateArmRequest(), isLeftButtonPressed: true);
        Assert.IsFalse(handshake.TryStart(
            CreateSignal(
                PreviewDirectDragSignalAction.Start,
                x: 20,
                y: 20),
            isLeftButtonPressed: true,
            isPanModeEnabled: true,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
        Assert.IsFalse(handshake.IsArmed);

        handshake.TryArm(CreateArmRequest(), isLeftButtonPressed: true);
        Assert.IsTrue(handshake.TryCancel(CreateSignal(
            PreviewDirectDragSignalAction.Cancel,
            x: 10,
            y: 10)));
        Assert.IsFalse(handshake.IsArmed);

        handshake.TryArm(CreateArmRequest(), isLeftButtonPressed: true);
        handshake.Reset();
        Assert.IsFalse(handshake.IsArmed);
    }

    [TestMethod]
    public void WrongGestureIdAndChangedViewport_CannotStart()
    {
        PreviewDirectDragHandshake handshake = new();
        handshake.TryArm(CreateArmRequest(), isLeftButtonPressed: true);

        Assert.IsFalse(handshake.TryStart(
            CreateSignal(
                PreviewDirectDragSignalAction.Start,
                x: 20,
                y: 20) with
            {
                GestureId = "FFEEDDCCBBAA99887766554433221100"
            },
            isLeftButtonPressed: true,
            isPanModeEnabled: false,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
        Assert.IsTrue(handshake.IsArmed);

        Assert.IsFalse(handshake.TryStart(
            CreateSignal(
                PreviewDirectDragSignalAction.Start,
                x: 20,
                y: 20) with
            {
                ViewportWidth = 501
            },
            isLeftButtonPressed: true,
            isPanModeEnabled: false,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
        Assert.IsFalse(handshake.IsArmed);
    }

    [TestMethod]
    public void ToolbarAndArtworkUseTheSameValidatedPngDragPipeline()
    {
        string source = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "ui",
                "MainWindow.xaml.cs"));

        StringAssert.Contains(
            source,
            "PreviewDragRequestOrigin.Artwork");
        StringAssert.Contains(
            source,
            "PreviewDragRequestOrigin.Toolbar");
        Assert.AreEqual(
            2,
            CountOccurrences(
                source,
                "CancelPendingDirectArtworkDrag();"));
        Assert.AreEqual(
            1,
            CountOccurrences(
                source,
                "_previewDragFileStore.TryCreate(pngPayload)"));
        Assert.AreEqual(
            1,
            CountOccurrences(
                source,
                "_previewDragDataObjectFactory.Create("));
    }

    private static PreviewPointerGestureInput CreateGesture() =>
        new(
            Button: 0,
            StartedOnArtwork: true,
            IsPrimary: true,
            IsMouse: true,
            ControlHeld: false,
            ShiftHeld: false,
            AltHeld: true,
            MetaHeld: false,
            SpaceHeld: false,
            PanModeEnabled: false);

    private static PreviewDirectDragArmRequest CreateArmRequest() =>
        new(
            GestureId,
            CreateGesture(),
            X: 10,
            Y: 10,
            ViewportWidth: 500,
            ViewportHeight: 300);

    private static PreviewDirectDragSignal CreateSignal(
        PreviewDirectDragSignalAction action,
        double x,
        double y) =>
        new(
            action,
            GestureId,
            x,
            y,
            ViewportWidth: 500,
            ViewportHeight: 300);

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(
                   value,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
