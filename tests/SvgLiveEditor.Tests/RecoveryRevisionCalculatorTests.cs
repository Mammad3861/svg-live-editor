using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class RecoveryRevisionCalculatorTests
{
    [TestMethod]
    public void RestoredSnapshotRevision_RemainsMonotonicAcrossProcesses()
    {
        Assert.AreEqual(
            1_001,
            RecoveryRevisionCalculator.Calculate(
                baselineRevision: 1_000,
                loadedSourceRevision: 1,
                currentSourceRevision: 2));
        Assert.AreEqual(
            1_003,
            RecoveryRevisionCalculator.Calculate(
                baselineRevision: 1_000,
                loadedSourceRevision: 1,
                currentSourceRevision: 4));
    }

    [TestMethod]
    public void NewDocumentStartsRecoveryRevisionAtZero()
    {
        Assert.AreEqual(
            0,
            RecoveryRevisionCalculator.Calculate(
                baselineRevision: 0,
                loadedSourceRevision: 42,
                currentSourceRevision: 42));
        Assert.AreEqual(
            1,
            RecoveryRevisionCalculator.Calculate(
                baselineRevision: 0,
                loadedSourceRevision: 42,
                currentSourceRevision: 43));
    }
}
