namespace SvgLiveEditor.Services;

public sealed class PreviewNavigationPolicy
{
    public bool IsAllowed(string uri, bool isHostPreviewRequested)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return uri.Equals("about:blank", StringComparison.OrdinalIgnoreCase)
            || (isHostPreviewRequested
                && uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase));
    }
}
