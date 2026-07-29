using System.Windows.Input;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class ApplicationShortcutResolverTests
{
    [TestMethod]
    public void BothWordWrapGesturesResolveToTheSameCommand()
    {
        ApplicationShortcut? altZ = ApplicationShortcutResolver.Resolve(
            ModifierKeys.Alt,
            Key.Z);
        ApplicationShortcut? controlAltW = ApplicationShortcutResolver.Resolve(
            ModifierKeys.Control | ModifierKeys.Alt,
            Key.W);

        Assert.AreEqual(ApplicationShortcut.ToggleWordWrap, altZ);
        Assert.AreEqual(altZ, controlAltW);
    }

    [TestMethod]
    public void SimilarGesturesDoNotToggleWordWrap()
    {
        Assert.IsNull(ApplicationShortcutResolver.Resolve(ModifierKeys.Control, Key.W));
        Assert.IsNull(ApplicationShortcutResolver.Resolve(ModifierKeys.Alt, Key.W));
        Assert.IsNull(ApplicationShortcutResolver.Resolve(ModifierKeys.None, Key.Z));
    }

    [TestMethod]
    public void ControlAltN_OpensTemplateGalleryWithoutConflictingWithNew()
    {
        Assert.AreEqual(
            ApplicationShortcut.NewFromTemplate,
            ApplicationShortcutResolver.Resolve(
                ModifierKeys.Control | ModifierKeys.Alt,
                Key.N));
        Assert.IsNull(ApplicationShortcutResolver.Resolve(
            ModifierKeys.Control,
            Key.N));
    }
}
