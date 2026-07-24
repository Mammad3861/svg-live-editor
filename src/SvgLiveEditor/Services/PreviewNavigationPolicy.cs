namespace SvgLiveEditor.Services;

public sealed class PreviewNavigationPolicy
{
    public const string TrustedPreviewDocumentPrefix = "data:text/html;charset=utf-8;base64,";

    public bool IsAllowed(string uri, bool isHostPreviewRequested)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return uri.Equals("about:blank", StringComparison.OrdinalIgnoreCase)
            || (isHostPreviewRequested
                && IsTrustedPreviewDocument(uri));
    }

    public bool IsTrustedPreviewDocument(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return uri.StartsWith(
            TrustedPreviewDocumentPrefix,
            StringComparison.OrdinalIgnoreCase);
    }

    public bool IsTrustedWebMessageSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // NavigateToString documents have an opaque about:blank origin in WebView2.
        // A per-navigation token separately binds a message to the current host page.
        return source.Equals("about:blank", StringComparison.OrdinalIgnoreCase);
    }
}
