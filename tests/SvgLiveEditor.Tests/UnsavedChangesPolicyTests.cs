using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class UnsavedChangesPolicyTests
{
    private readonly UnsavedChangesPolicy _policy = new();

    [TestMethod]
    public void CanProceed_WhenDocumentIsUnmodified()
    {
        Assert.IsTrue(_policy.CanProceed(false, UnsavedChangesChoice.Cancel, saveSucceeded: false));
    }

    [TestMethod]
    public void CanProceed_WhenUserDiscardsChanges()
    {
        Assert.IsTrue(_policy.CanProceed(true, UnsavedChangesChoice.Discard, saveSucceeded: false));
    }

    [TestMethod]
    public void CannotProceed_WhenUserCancels()
    {
        Assert.IsFalse(_policy.CanProceed(true, UnsavedChangesChoice.Cancel, saveSucceeded: false));
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void SaveChoice_DependsOnSaveResult(bool saveSucceeded, bool expected)
    {
        Assert.AreEqual(expected, _policy.CanProceed(true, UnsavedChangesChoice.Save, saveSucceeded));
    }
}
