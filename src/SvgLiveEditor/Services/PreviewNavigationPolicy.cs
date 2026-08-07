namespace SvgLiveEditor.Services;

public sealed class PreviewNavigationPolicy
{
    public const string TrustedPreviewDocumentPrefix = "data:text/html;charset=utf-8;base64,";

    public bool IsAllowed(string uri, bool isHostPreviewRequested)
    {
        ArgumentNullException.ThrowIfNull(uri);

        // NavigateToString is reported as about:blank by current WebView2
        // runtimes. That opaque URL is trusted only while the host is actively
        // starting the next preview document. Otherwise an empty about:blank
        // navigation could replace an attested preview while the WPF host kept
        // displaying its previous Ready state.
        return isHostPreviewRequested
            && (uri.Equals("about:blank", StringComparison.OrdinalIgnoreCase)
                || IsTrustedPreviewDocument(uri));
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
