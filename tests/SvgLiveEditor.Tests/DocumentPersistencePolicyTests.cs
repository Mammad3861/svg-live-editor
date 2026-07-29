using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class DocumentPersistencePolicyTests
{
    [TestMethod]
    public void DebounceIntervalsMatchDocumentedBehavior()
    {
        Assert.AreEqual(
            TimeSpan.FromMilliseconds(1500),
            DocumentPersistencePolicy.RecoveryDelay);
        Assert.AreEqual(
            TimeSpan.FromSeconds(2),
            DocumentPersistencePolicy.AutoSaveDelay);
    }

    [TestMethod]
    public void AutoSaveRequiresEnabledEligibleModifiedNamedDocument()
    {
        Assert.IsTrue(DocumentPersistencePolicy.ShouldScheduleAutoSave(
            autoSaveEnabled: true,
            documentIsEligible: true,
            isModified: true,
            currentPath: @"D:\work\sample.svg"));
        Assert.IsFalse(DocumentPersistencePolicy.ShouldScheduleAutoSave(
            false, true, true, @"D:\work\sample.svg"));
        Assert.IsFalse(DocumentPersistencePolicy.ShouldScheduleAutoSave(
            true, false, true, @"D:\work\sample.svg"));
        Assert.IsFalse(DocumentPersistencePolicy.ShouldScheduleAutoSave(
            true, true, false, @"D:\work\sample.svg"));
        Assert.IsFalse(DocumentPersistencePolicy.ShouldScheduleAutoSave(
            true, true, true, currentPath: null));
    }
}
