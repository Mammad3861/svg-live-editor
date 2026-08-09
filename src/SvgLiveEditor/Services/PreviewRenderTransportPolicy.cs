using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public enum PreviewRenderTransport
{
    Navigation,
    InPlaceImage
}

public sealed class PreviewRenderTransportPolicy
{
    public PreviewRenderTransport Decide(
        PreviewRenderRequest request,
        PreviewRenderRequest? lastSuccessful,
        bool hasVisiblePreview,
        bool hasTrustedPageToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequiresNavigation
            || !hasVisiblePreview
            || !hasTrustedPageToken
            || lastSuccessful is null
            || lastSuccessful.CanvasSize != request.CanvasSize
            || lastSuccessful.VisualDocument.Viewport
                != request.VisualDocument.Viewport)
        {
            return PreviewRenderTransport.Navigation;
        }

        return PreviewRenderTransport.InPlaceImage;
    }
}
