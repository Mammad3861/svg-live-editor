using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgVisualResizeTests
{
    private readonly SvgVisualResizeService _resizeService = new();
    private readonly SvgVisualResizeHandleService _handleService = new();

    [TestMethod]
    [DataRow("rect", 8)]
    [DataRow("ellipse", 8)]
    [DataRow("circle", 4)]
    [DataRow("line", 2)]
    public void EligibleShapesReceiveExpectedHandles(
        string elementName,
        int expectedCount)
    {
        SvgVisualElement element = BuildElement(SourceFor(elementName));

        IReadOnlyList<SvgResizeHandleDefinition> handles =
            _handleService.Create(element);

        Assert.HasCount(expectedCount, handles);
        Assert.HasCount(
            expectedCount,
            handles.Select(item => item.Handle).Distinct());
    }

    [TestMethod]
    [DataRow("text")]
    [DataRow("path")]
    [DataRow("polygon")]
    [DataRow("polyline")]
    public void TextAndInspectionOnlyElementsHaveNoResizeHandles(
        string elementName)
    {
        SvgVisualElement element = BuildElement(SourceFor(elementName));

        Assert.IsEmpty(_handleService.Create(element));
    }

    [TestMethod]
    public void RectangleCornerKeepsOppositeCornerAnchored()
    {
        SvgVisualElement element = BuildElement(SourceFor("rect"));

        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.TopLeft,
            new SvgVisualPoint(5, 6),
            preserveAspectRatio: false,
            out SvgVisualShapeGeometry resized,
            out string? error), error);

        Assert.AreEqual(new SvgVisualBounds(5, 6, 110, 70), resized.Bounds);
    }

    [TestMethod]
    public void RectangleEdgeChangesOneAxis()
    {
        SvgVisualElement element = BuildElement(SourceFor("rect"));

        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.Right,
            new SvgVisualPoint(150, 999),
            preserveAspectRatio: false,
            out SvgVisualShapeGeometry resized,
            out string? error), error);

        Assert.AreEqual(new SvgVisualBounds(10, 20, 150, 70), resized.Bounds);
    }

    [TestMethod]
    public void ShiftCornerPreservesRectangleAspectRatio()
    {
        SvgVisualElement element = BuildElement(SourceFor("rect"));

        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.BottomRight,
            new SvgVisualPoint(210, 90),
            preserveAspectRatio: true,
            out SvgVisualShapeGeometry resized,
            out string? error), error);

        Assert.AreEqual(200, resized.Bounds.Width, 0.000001);
        Assert.AreEqual(100, resized.Bounds.Height, 0.000001);
        Assert.AreEqual(10, resized.Bounds.Left, 0.000001);
        Assert.AreEqual(20, resized.Bounds.Top, 0.000001);
    }

    [TestMethod]
    public void ShiftCornerPreservesEllipseBoundingBoxAspectRatio()
    {
        SvgVisualElement element = BuildElement(SourceFor("ellipse"));

        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.TopLeft,
            new SvgVisualPoint(0, 0),
            preserveAspectRatio: true,
            out SvgVisualShapeGeometry resized,
            out string? error), error);

        Assert.AreEqual(2, resized.Bounds.Width / resized.Bounds.Height, 0.000001);
        Assert.AreEqual(70, resized.Bounds.Right, 0.000001);
        Assert.AreEqual(50, resized.Bounds.Bottom, 0.000001);
    }

    [TestMethod]
    public void RectangleHandlePointsCoverCornersAndEdges()
    {
        SvgVisualElement element = BuildElement(SourceFor("rect"));
        Dictionary<SvgResizeHandle, SvgVisualPoint> handles =
            _handleService.Create(element).ToDictionary(
                item => item.Handle,
                item => item.Point);

        Assert.AreEqual(new SvgVisualPoint(10, 20),
            handles[SvgResizeHandle.TopLeft]);
        Assert.AreEqual(new SvgVisualPoint(60, 20),
            handles[SvgResizeHandle.Top]);
        Assert.AreEqual(new SvgVisualPoint(110, 45),
            handles[SvgResizeHandle.Right]);
        Assert.AreEqual(new SvgVisualPoint(60, 70),
            handles[SvgResizeHandle.Bottom]);
        Assert.AreEqual(new SvgVisualPoint(10, 45),
            handles[SvgResizeHandle.Left]);
    }

    [TestMethod]
    public void EllipseEdgeUpdatesCenterAndRadius()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><ellipse id=\"target\" cx=\"50\" cy=\"40\" rx=\"20\" ry=\"10\"/></svg>";
        SvgVisualElement element = BuildElement(source);
        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.Right,
            new SvgVisualPoint(80, 40),
            preserveAspectRatio: false,
            out SvgVisualShapeGeometry resized,
            out string? error), error);

        SvgAttributeEditResult edit =
            _resizeService.CreateEdit(source, element, resized);

        Assert.IsTrue(edit.IsSuccess, edit.ErrorMessage);
        string updated = edit.Edit!.Apply(source);
        StringAssert.Contains(updated, "cx=\"55\"");
        StringAssert.Contains(updated, "rx=\"25\"");
        StringAssert.Contains(updated, "cy=\"40\"");
        StringAssert.Contains(updated, "ry=\"10\"");
    }

    [TestMethod]
    public void CircleCardinalHandleKeepsOppositeEdgeAndTrueCircle()
    {
        SvgVisualElement element = BuildElement(SourceFor("circle"));
        (SvgResizeHandle Handle, SvgVisualPoint Pointer,
            Func<SvgVisualBounds, double> Opposite, double Expected)[] cases =
        [
            (SvgResizeHandle.Left, new SvgVisualPoint(20, 40),
                bounds => bounds.Right, 50),
            (SvgResizeHandle.Right, new SvgVisualPoint(60, 40),
                bounds => bounds.Left, 30),
            (SvgResizeHandle.Top, new SvgVisualPoint(40, 20),
                bounds => bounds.Bottom, 50),
            (SvgResizeHandle.Bottom, new SvgVisualPoint(40, 60),
                bounds => bounds.Top, 30)
        ];

        foreach ((SvgResizeHandle handle, SvgVisualPoint pointer,
                 Func<SvgVisualBounds, double> opposite, double expected)
                 in cases)
        {
            Assert.IsTrue(_resizeService.TryCalculate(
                element,
                handle,
                pointer,
                preserveAspectRatio: false,
                out SvgVisualShapeGeometry resized,
                out string? error), error);
            Assert.AreEqual(expected, opposite(resized.Bounds), 0.000001);
            Assert.AreEqual(
                resized.Bounds.Width,
                resized.Bounds.Height,
                0.000001);
        }
    }

    [TestMethod]
    public void LineEndpointUpdatesOnlyThatEndpoint()
    {
        string source = SourceFor("line");
        SvgVisualElement element = BuildElement(source);
        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.Start,
            new SvgVisualPoint(-5, 7),
            preserveAspectRatio: false,
            out SvgVisualShapeGeometry resized,
            out string? error), error);

        SvgAttributeEditResult edit =
            _resizeService.CreateEdit(source, element, resized);

        string updated = edit.Edit!.Apply(source);
        StringAssert.Contains(updated, "x1=\"-5\"");
        StringAssert.Contains(updated, "y1=\"7\"");
        StringAssert.Contains(updated, "x2=\"30\"");
        StringAssert.Contains(updated, "y2=\"40\"");

        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.End,
            new SvgVisualPoint(45, 55),
            preserveAspectRatio: false,
            out SvgVisualShapeGeometry resizedEnd,
            out error), error);
        string endUpdated = _resizeService.CreateEdit(
            source,
            element,
            resizedEnd).Edit!.Apply(source);
        StringAssert.Contains(endUpdated, "x1=\"1\"");
        StringAssert.Contains(endUpdated, "y1=\"2\"");
        StringAssert.Contains(endUpdated, "x2=\"45\"");
        StringAssert.Contains(endUpdated, "y2=\"55\"");
    }

    [TestMethod]
    public void CrossingOppositeEdgeClampsWithoutFlipping()
    {
        SvgVisualElement element = BuildElement(SourceFor("rect"));

        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.Left,
            new SvgVisualPoint(500, 20),
            preserveAspectRatio: false,
            out SvgVisualShapeGeometry resized,
            out string? error), error);

        Assert.AreEqual(
            SvgVisualResizeService.MinimumDimension,
            resized.Bounds.Width,
            0.000001);
        Assert.AreEqual(110, resized.Bounds.Right, 0.000001);
    }

    [TestMethod]
    public void SourceEditPreservesPxAndUnrelatedPersianSource()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\">\n<!-- فارسی -->\n<rect id=\"target\" x=\"10px\" y=\"20\" width=\"100px\" height=\"50\" rx=\"7\" data-note=\"بهروز\"/>\n</svg>";
        SvgVisualElement element = BuildElement(source);
        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.BottomRight,
            new SvgVisualPoint(140, 90),
            preserveAspectRatio: false,
            out SvgVisualShapeGeometry resized,
            out string? error), error);

        SvgAttributeEditResult result =
            _resizeService.CreateEdit(source, element, resized);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string updated = result.Edit!.Apply(source);
        StringAssert.Contains(updated, "width=\"130px\"");
        StringAssert.Contains(updated, "height=\"70\"");
        StringAssert.Contains(updated, "rx=\"7\"");
        StringAssert.Contains(updated, "data-note=\"بهروز\"");
        StringAssert.Contains(updated, "<!-- فارسی -->");
    }

    [TestMethod]
    public void MissingZeroPositionIsAddedOnlyWhenResizeRequiresIt()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"target\" width=\"10\" height=\"20\"/></svg>";
        SvgVisualElement element = BuildElement(source);
        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.Left,
            new SvgVisualPoint(-5, 0),
            preserveAspectRatio: false,
            out SvgVisualShapeGeometry resized,
            out string? error), error);

        string updated = _resizeService.CreateEdit(source, element, resized)
            .Edit!.Apply(source);

        StringAssert.Contains(updated, "x=\"-5\"");
        Assert.IsFalse(updated.Contains(" y=", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FormattingIsInvariantBoundedAndAvoidsScientificNotation()
    {
        string source = SourceFor("rect");
        SvgVisualElement element = BuildElement(source);
        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.Right,
            new SvgVisualPoint(123.4567894, 0),
            preserveAspectRatio: false,
            out SvgVisualShapeGeometry resized,
            out string? error), error);

        string updated = _resizeService.CreateEdit(source, element, resized)
            .Edit!.Apply(source);

        StringAssert.Contains(updated, "width=\"113.456789\"");
        Assert.IsFalse(updated.Contains('E'));
    }

    [TestMethod]
    public void NonZeroViewBoxZoomAndScrollMapToTheSameResizeGeometry()
    {
        SvgVisualViewport viewport = new(
            -100,
            50,
            400,
            200,
            SvgPreserveAspectRatio.Default);
        PreviewSvgCoordinateMapper mapper = new();

        Assert.IsTrue(mapper.TryMap(
            viewport,
            new PreviewImageMetrics(10, 20, 800, 400),
            new SvgVisualPoint(250, 200),
            out SvgMappedPreviewPoint atManualZoom));
        Assert.IsTrue(mapper.TryMap(
            viewport,
            new PreviewImageMetrics(-110, -70, 400, 200),
            new SvgVisualPoint(10, 20),
            out SvgMappedPreviewPoint afterScroll));

        Assert.AreEqual(20, atManualZoom.Point.X, 0.000001);
        Assert.AreEqual(140, atManualZoom.Point.Y, 0.000001);
        Assert.AreEqual(20, afterScroll.Point.X, 0.000001);
        Assert.AreEqual(140, afterScroll.Point.Y, 0.000001);
        Assert.AreEqual(3, atManualZoom.HitTolerance, 0.000001);
        Assert.AreEqual(6, afterScroll.HitTolerance, 0.000001);
    }

    [TestMethod]
    public void ResizeAppliesAsOneUndoRedoOperation()
    {
        string source = SourceFor("rect");
        SvgVisualElement element = BuildElement(source);
        Assert.IsTrue(_resizeService.TryCalculate(
            element,
            SvgResizeHandle.BottomRight,
            new SvgVisualPoint(150, 90),
            preserveAspectRatio: false,
            out SvgVisualShapeGeometry resized,
            out string? error), error);
        SvgAttributeEditResult edit =
            _resizeService.CreateEdit(source, element, resized);
        TextDocument document = new(source);
        document.UndoStack.MarkAsOriginalFile();

        new AvalonEditDocumentEditService().Apply(document, edit.Edit!);
        string changed = document.Text;
        document.UndoStack.Undo();

        Assert.AreEqual(source, document.Text);
        Assert.IsFalse(document.UndoStack.CanUndo);
        document.UndoStack.Redo();
        Assert.AreEqual(changed, document.Text);
    }

    [TestMethod]
    public void ResizedPersianSourceRoundTripsThroughAutoSaveAndRecovery()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.Resize.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            const string source =
                "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"target\" x=\"10\" y=\"20\" width=\"100\" height=\"50\"/><text x=\"2\" y=\"9\">بهروز ۱۲۳</text></svg>";
            SvgVisualElement element = BuildElement(source);
            Assert.IsTrue(_resizeService.TryCalculate(
                element,
                SvgResizeHandle.Right,
                new SvgVisualPoint(140, 20),
                preserveAspectRatio: false,
                out SvgVisualShapeGeometry resized,
                out string? error), error);
            string updated = _resizeService.CreateEdit(
                source,
                element,
                resized).Edit!.Apply(source);
            Assert.IsTrue(new SvgValidationService().Validate(updated).IsValid);

            string documentPath = Path.Combine(directory, "document.svg");
            new Utf8FileService().WriteAllText(documentPath, source);
            AutoSavePrepareResult prepared =
                new AutoSaveFileService().Prepare(documentPath, updated);
            Assert.IsTrue(prepared.Succeeded, prepared.ErrorMessage);
            using (PreparedAutoSave write = prepared.PreparedWrite!)
            {
                Assert.IsTrue(write.Commit().Succeeded);
            }
            Assert.AreEqual(
                updated,
                new Utf8FileService().ReadAllText(documentPath));

            string recoveryDirectory = Path.Combine(directory, "Recovery");
            RecoverySnapshotStore recovery = new(
                recoveryDirectory,
                new Utf8FileService(),
                new SafeDocumentPathService());
            RecoverySnapshot snapshot = RecoverySnapshotStore.CreateSnapshot(
                RecoverySnapshotStore.CreateSnapshotId(),
                originalPath: null,
                "Untitled.svg",
                updated,
                revision: 2,
                DateTimeOffset.UtcNow);
            Assert.IsTrue(recovery.TryWrite(snapshot).Succeeded);
            Assert.AreEqual(
                updated,
                recovery.LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
                    .Single().Snapshot.Source);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static SvgVisualElement BuildElement(string source)
    {
        SvgDocumentIndexResult index = new SvgDocumentIndexService().Build(source);
        Assert.IsTrue(index.IsIndexed, index.IndexError);
        SvgVisualDocument visual = new SvgVisualGeometryIndexService().Build(
            index.Document!,
            new SvgCanvasSizeReader().Read(source),
            source);
        return visual.Elements.Single(element =>
            element.SourceElement.Id == "target");
    }

    private static string SourceFor(string elementName) => elementName switch
    {
        "rect" =>
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"target\" x=\"10\" y=\"20\" width=\"100\" height=\"50\"/></svg>",
        "circle" =>
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle id=\"target\" cx=\"40\" cy=\"40\" r=\"10\"/></svg>",
        "ellipse" =>
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><ellipse id=\"target\" cx=\"50\" cy=\"40\" rx=\"20\" ry=\"10\"/></svg>",
        "line" =>
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><line id=\"target\" x1=\"1\" y1=\"2\" x2=\"30\" y2=\"40\"/></svg>",
        "text" =>
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\" x=\"10\" y=\"20\">text</text></svg>",
        "path" =>
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><path id=\"target\" d=\"M 1 2 L 30 40\"/></svg>",
        "polygon" =>
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><polygon id=\"target\" points=\"1,2 30,40 20,10\"/></svg>",
        "polyline" =>
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><polyline id=\"target\" points=\"1,2 30,40 20,10\"/></svg>",
        _ => throw new ArgumentOutOfRangeException(nameof(elementName))
    };
}
