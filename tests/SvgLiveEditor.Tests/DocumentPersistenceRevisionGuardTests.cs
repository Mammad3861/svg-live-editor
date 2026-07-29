using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class DocumentPersistenceRevisionGuardTests
{
    [TestMethod]
    public void ExactDocumentRevision_CanCommit()
    {
        Assert.IsTrue(DocumentPersistenceRevisionGuard.IsCurrent(
            capturedSession: 4,
            currentSession: 4,
            capturedRevision: 12,
            currentRevision: 12,
            capturedSource: "سلام",
            currentSource: "سلام",
            capturedPath: @"D:\work\sample.svg",
            currentPath: @"d:\work\sample.svg"));
    }

    [TestMethod]
    public void NewerEditDocumentOrPath_InvalidatesStagedWrite()
    {
        Assert.IsFalse(DocumentPersistenceRevisionGuard.IsCurrent(
            4, 4, 12, 13, "old", "new", "a.svg", "a.svg"));
        Assert.IsFalse(DocumentPersistenceRevisionGuard.IsCurrent(
            4, 5, 12, 12, "same", "same", "a.svg", "a.svg"));
        Assert.IsFalse(DocumentPersistenceRevisionGuard.IsCurrent(
            4, 4, 12, 12, "same", "same", "a.svg", "b.svg"));
        Assert.IsFalse(DocumentPersistenceRevisionGuard.IsCurrent(
            4, 4, 12, 12, "old", "new", "a.svg", "a.svg"));
    }
}
