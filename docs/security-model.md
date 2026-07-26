# Preview security model

SvgLiveEditor treats source files as hostile input and uses several independent controls before pixels reach the preview.

## Validation boundary

`SvgValidationService` parses with .NET `XmlReader` using `DtdProcessing.Prohibit`, a null `XmlResolver`, and a ten-million-character document limit. It requires an SVG root in `http://www.w3.org/2000/svg` and rejects:

- DTD/entity declarations and XML processing instructions;
- elements outside the SVG namespace;
- `script`, `foreignObject`, hyperlinks, and active embedded web/media elements;
- inline event-handler attributes and `xml:base`;
- `<style>` elements and CSS imports;
- non-fragment `href`/`src` values and non-fragment CSS `url(...)` values.

These rules are conservative. Unsupported content produces a visible validation error and does not replace the last valid preview.

## Document inspector boundary

The element tree is built only after `SvgValidationService` accepts the current source. The index stores source offsets and hierarchy; it does not expose a browser DOM and does not add IDs or metadata. Supported property changes replace one existing attribute value or insert one missing attribute in the selected start tag, then pass through the same validator and preview pipeline. The application never serializes the full XML document for an inspector edit, so unrelated whitespace, comments, attribute order, and UTF-8 text remain untouched.

## Rendering boundary

Validated source is encoded as UTF-8 and then Base64. A trusted, fixed HTML host loads it through an `<img src="data:image/svg+xml;base64,...">`; raw source is never concatenated into markup or an iframe `srcdoc`. Image-mode SVG does not provide an interactive document scripting surface.

The host Content Security Policy defaults all sources to none and permits only the Base64 data image, fixed inline host CSS, and one fixed app-owned interaction script authorized by its exact SHA-256 hash. The same LF-normalized script bytes are used both for hashing and insertion. The script handles preview wheel scrolling, pointer panning, and bounded full-image PNG rendering. It does not receive or insert SVG source, expose an SVG DOM, call `eval`, load code, navigate, access files, or make network requests. Connections, fonts, media, objects, forms, and base URL changes remain disabled.

Copy Preview renders only the already validated `<img>` into a transparent, app-owned off-screen canvas at dimensions selected by the host. It does not capture the viewport, checkerboard, scrollbars, window chrome, editor, or Inspector. The requested output preserves aspect ratio and is bounded to 4096 pixels per side and 8,000,000 pixels total. Oversized SVG canvases are scaled down before a browser canvas or host bitmap is allocated. The operation does not navigate or modify preview zoom/scroll state.

## WebView2 boundary

WebView2 enables scripting only so the CSP-hashed trusted host interaction script can run. The SVG remains a Base64 image rather than a DOM document, so SVG scripts cannot execute in the host context. Web messages use exact command-specific schemas and the unpredictable per-navigation token. The application rejects malformed messages, unknown commands, unexpected fields, stale tokens, unexpected sources, out-of-range values, and unsupported directions. `NavigateToString` gives the trusted page an opaque `about:blank` message origin, so source validation is paired with that token rather than trusting the origin alone.

Host-to-page messages can only update the existing image's zoom/normalized viewport, set Pan mode, or request a bounded PNG from the existing image. Page-to-host messages can only report normalized viewport state, request a bounded zoom/Pan command, return a fixed PNG error, or return one matching PNG response. A PNG response must match the pending navigation token and random request ID and have the exact schema, `image/png` MIME type, requested dimensions, bounded Base64 and decoded lengths, PNG signature, valid chunk framing, one matching IHDR, image data, and a terminal IEND before it reaches the Windows clipboard decoder. Only one PNG request can be pending, and navigation cancels it. No arbitrary command dispatch or host object is available.

Script dialogs, host objects, permissions, downloads, pop-ups, external navigation, context menus, developer tools, pinch zoom, password saving, and autofill remain disabled. WebView2's Ctrl+Wheel dispatch is enabled because disabling its zoom controls suppresses the physical wheel event before the trusted page can receive it. The page captures Ctrl+Wheel with a non-passive listener, prevents native document zoom, and asks the host to resize only the SVG image; a host-side guard keeps the WebView zoom factor at 100%. Resource interception permits only the initial blank document, the generated `data:text/html` host, and the `data:image/svg+xml;base64` image, and returns a blocked response for everything else. No host object is exposed to SVG content.

The MVP uses the installed Evergreen WebView2 Runtime. A self-contained .NET publish does not bundle that runtime.

WebView2 profile and cache data is stored under the current user's local application-data directory, not beside the executable. The publish script builds in a clean staging directory and refuses to package an `EBWebView` or `*.WebView2` directory, preventing local browsing state or locked runtime files from entering the release archive.

## Residual risk

Complex graphics can consume CPU or memory even without active content. The XML character cap and PNG output caps limit source and bitmap size but are not complete rendering-complexity or rendering-time limits. PNG structural validation does not independently reimplement every decoder check; WPF's image decoder performs the final decode after the bridge-specific bounds and structure checks. Users should avoid opening deliberately adversarial files from unknown sources, and future releases should consider configurable file-size and rendering-time limits.
