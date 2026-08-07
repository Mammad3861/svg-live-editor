using System.Text;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewAuthoringLifecycleTests
{
    private const string Token = "00112233445566778899AABBCCDDEEFF";
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgElementCreationService _creationService = new();
    private readonly SvgVisualGeometryIndexService _geometryService = new();

    [TestMethod]
    public void RootAndNestedEmptyGroupCreationRetainArtworkInIsolatedPreviewRequest()
    {
        const string original =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 150\"><rect id=\"art\" width=\"300\" height=\"150\" fill=\"#16a34a\"/></svg>";
        SvgDocumentIndex firstDocument = Build(original);
        SvgAuthoringEditResult rootGroup = _creationService.CreateEdit(
            original,
            firstDocument,
            selection: null,
            SvgCreateDestination.SvgRoot,
            SvgCreateElementKind.Group,
            new SvgCanvasSize(300, 150));
        string withRootGroup = rootGroup.Edit!.Apply(original);
        SvgDocumentIndex rootGroupDocument = Build(withRootGroup);
        SvgElementNode group = rootGroupDocument.FindBestMatch(
            rootGroup.PreferredSelection!)!;

        SvgAuthoringEditResult nestedGroup = _creationService.CreateEdit(
            withRootGroup,
            rootGroupDocument,
            group,
            SvgCreateDestination.SelectedContext,
            SvgCreateElementKind.Group,
            new SvgCanvasSize(300, 150));
        string current = nestedGroup.Edit!.Apply(withRootGroup);

        StringAssert.Contains(current, "id=\"art\"");
        Assert.AreEqual(2, current.Split("<g id=", StringSplitOptions.None).Length - 1);
        PreviewRenderRequest request = CreateRequest(current, sourceRevision: 3);
        string html = new PreviewHtmlBuilder().Build(
            request.Svg,
            300,
            150,
            Token,
            sourceRevision: request.SourceRevision,
            visualViewport: request.VisualDocument.Viewport);

        Assert.AreEqual(current, DecodeEmbeddedSvg(html));
        StringAssert.Contains(DecodeEmbeddedSvg(html), "fill=\"#16a34a\"");
    }

    [TestMethod]
    public void RapidCreateReparentReorderUndoRedoKeepsOnlyLatestValidRevision()
    {
        const string original =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 150\"><g id=\"left\"><circle id=\"dot\" cx=\"50\" cy=\"50\" r=\"20\"/></g><g id=\"right\"><rect id=\"box\" x=\"100\" y=\"20\" width=\"80\" height=\"60\"/></g></svg>";
        PreviewNavigationCoordinator coordinator = new();
        PreviewRenderRequest active = Enqueue(coordinator, original, 1);
        Assert.AreEqual(active, coordinator.TryBeginNext());

        SvgDocumentIndex originalDocument = Build(original);
        SvgAuthoringEditResult created = _creationService.CreateEdit(
            original,
            originalDocument,
            Find(originalDocument, "right"),
            SvgCreateDestination.SelectedContext,
            SvgCreateElementKind.Group,
            new SvgCanvasSize(300, 150));
        string afterCreate = created.Edit!.Apply(original);
        Enqueue(coordinator, afterCreate, 2);

        SvgDocumentIndex createdDocument = Build(afterCreate);
        SvgAuthoringEditResult reparented = new SvgLayerReparentService().CreateDropEdit(
            afterCreate,
            createdDocument,
            Find(createdDocument, "dot"),
            Find(createdDocument, "right"),
            SvgLayerDropPlacement.Inside);
        string afterReparent = reparented.Edit!.Apply(afterCreate);
        Enqueue(coordinator, afterReparent, 3);

        SvgDocumentIndex reparentedDocument = Build(afterReparent);
        SvgLayerOrderEditResult reordered = new SvgLayerOrderService().CreateEdit(
            afterReparent,
            reparentedDocument,
            Find(reparentedDocument, "box"),
            SvgLayerOrderCommand.BringToFront);
        Assert.IsTrue(reordered.IsSuccess, reordered.ErrorMessage);
        string afterReorder = reordered.Edit!.Apply(afterReparent);
        PreviewRenderRequest latest = Enqueue(coordinator, afterReorder, 4);

        Assert.IsFalse(coordinator.TryEnqueue(
            latest.SourceRevision,
            latest.Svg,
            latest.CanvasSize,
            latest.VisualDocument,
            latest.ZoomState,
            latest.Viewport,
            force: false,
            out _));
        Assert.IsTrue(coordinator.TryComplete(
            active.Revision,
            isSuccess: true,
            out bool activeWasLatest));
        Assert.IsFalse(activeWasLatest);
        Assert.AreEqual(latest, coordinator.TryBeginNext());

        PreviewRenderReadiness readiness = new();
        readiness.Begin(latest.Revision, latest.SourceRevision);
        Assert.AreEqual(
            PreviewRenderReadinessResult.Waiting,
            readiness.RecordImage(latest.Revision, Loaded(latest.SourceRevision)));
        Assert.AreEqual(
            PreviewRenderReadinessResult.Ready,
            readiness.RecordNavigation(latest.Revision, isSuccess: true));
        Assert.IsTrue(coordinator.TryComplete(
            latest.Revision,
            isSuccess: true,
            out bool latestWasLatest));
        Assert.IsTrue(latestWasLatest);

        PreviewRenderRequest undo = Enqueue(coordinator, afterReparent, 5);
        Assert.AreEqual(undo, coordinator.TryBeginNext());
        Assert.IsTrue(coordinator.TryComplete(
            undo.Revision,
            isSuccess: true,
            out _));
        PreviewRenderRequest redo = Enqueue(coordinator, afterReorder, 6);
        Assert.AreEqual(redo, coordinator.TryBeginNext());
        Assert.IsTrue(coordinator.TryComplete(
            redo.Revision,
            isSuccess: true,
            out _));
        Assert.AreEqual(afterReorder, redo.Svg);
    }

    [TestMethod]
    public void InvalidTransitionDoesNotReplaceLastValidAndNextValidRevisionCanRender()
    {
        const string valid =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام</text></svg>";
        const string invalid =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام</svg>";
        PreviewNavigationCoordinator coordinator = new();
        PreviewRenderRequest visible = Enqueue(coordinator, valid, 10);
        Assert.AreEqual(visible, coordinator.TryBeginNext());
        Assert.IsTrue(coordinator.TryComplete(
            visible.Revision,
            isSuccess: true,
            out _));

        Assert.IsFalse(_indexService.Build(invalid).Validation.IsValid);
        Assert.IsNull(coordinator.TryBeginNext());

        const string validAgain =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام دنیا</text></svg>";
        PreviewRenderRequest recovered = Enqueue(coordinator, validAgain, 12);
        Assert.AreEqual(recovered, coordinator.TryBeginNext());
        StringAssert.Contains(recovered.Svg, "سلام دنیا");
    }

    private PreviewRenderRequest Enqueue(
        PreviewNavigationCoordinator coordinator,
        string source,
        long sourceRevision)
    {
        PreviewRenderRequest request = CreateRequest(source, sourceRevision);
        Assert.IsTrue(coordinator.TryEnqueue(
            request.SourceRevision,
            request.Svg,
            request.CanvasSize,
            request.VisualDocument,
            request.ZoomState,
            request.Viewport,
            force: false,
            out PreviewRenderRequest? enqueued));
        Assert.IsNotNull(enqueued);
        return enqueued;
    }

    private PreviewRenderRequest CreateRequest(string source, long sourceRevision)
    {
        SvgDocumentIndex document = Build(source);
        SvgCanvasSize canvas = new SvgCanvasSizeReader().Read(source);
        SvgVisualDocument visual = _geometryService.Build(document, canvas, source);
        return new PreviewRenderRequest(
            Revision: 0,
            sourceRevision,
            source,
            canvas,
            visual,
            PreviewZoomState.Fit,
            PreviewViewportPosition.Center);
    }

    private SvgDocumentIndex Build(string source)
    {
        SvgDocumentIndexResult result = _indexService.Build(source);
        Assert.IsTrue(result.Validation.IsValid, result.Validation.Message);
        Assert.IsNotNull(result.Document);
        return result.Document;
    }

    private static SvgElementNode Find(SvgDocumentIndex document, string id) =>
        document.Elements.Single(element => element.Id == id);

    private static PreviewImageLoadMessage Loaded(long sourceRevision) =>
        new(
            PreviewImageLoadState.Loaded,
            sourceRevision,
            NaturalWidth: 300,
            NaturalHeight: 150,
            RenderedWidth: 300,
            RenderedHeight: 150,
            ViewportWidth: 640,
            ViewportHeight: 480);

    private static string DecodeEmbeddedSvg(string html)
    {
        const string marker = "src=\"data:image/svg+xml;base64,";
        int start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0);
        start += marker.Length;
        int end = html.IndexOf('"', start);
        Assert.IsTrue(end > start);
        return Encoding.UTF8.GetString(Convert.FromBase64String(html[start..end]));
    }
}
