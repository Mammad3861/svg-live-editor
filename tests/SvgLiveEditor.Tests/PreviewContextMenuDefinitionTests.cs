using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewContextMenuDefinitionTests
{
    [TestMethod]
    public void MenuContainsOnlyFixedAppOwnedCommands()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                PreviewContextMenuCommand.CopyPreviewAsPng,
                PreviewContextMenuCommand.Fit,
                PreviewContextMenuCommand.ResetZoom
            },
            PreviewContextMenuDefinition.Items
                .Select(item => item.Command)
                .ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "Copy Preview as PNG",
                "Fit",
                "Reset Zoom"
            },
            PreviewContextMenuDefinition.Items
                .Select(item => item.Header)
                .ToArray());
    }
}
