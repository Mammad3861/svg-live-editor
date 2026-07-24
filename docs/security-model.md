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

## Rendering boundary

Validated source is encoded as UTF-8 and then Base64. A trusted, fixed HTML host loads it through an `<img src="data:image/svg+xml;base64,...">`; raw source is never concatenated into markup or an iframe `srcdoc`. Image-mode SVG does not provide an interactive document scripting surface.

The host Content Security Policy defaults all sources to none and permits only the Base64 data image, fixed inline host CSS, and one fixed app-owned interaction script authorized by its exact SHA-256 hash. The script handles preview wheel scrolling, pointer panning, and reports only a zoom direction plus bounded pointer/viewport coordinates. It does not receive or insert SVG source, call `eval`, load code, navigate, access files, or make network requests. Connections, fonts, media, objects, forms, and base URL changes remain disabled.

## WebView2 boundary

WebView2 enables scripting only so the CSP-hashed trusted host interaction script can run. The SVG remains a Base64 image rather than a DOM document, so SVG scripts cannot execute in the host context. Web messaging is enabled only for host-to-application zoom requests; the application rejects malformed messages, unknown command types, unexpected fields, out-of-range coordinates, and unsupported directions. No arbitrary command dispatch or host-to-page data channel exists.

Script dialogs, host objects, permissions, downloads, pop-ups, external navigation, context menus, developer tools, browser zoom controls, password saving, and autofill remain disabled. Resource interception permits only the initial blank document, the generated `data:text/html` host, and the `data:image/svg+xml;base64` image, and returns a blocked response for everything else. No host object is exposed to SVG content.

The MVP uses the installed Evergreen WebView2 Runtime. A self-contained .NET publish does not bundle that runtime.

WebView2 profile and cache data is stored under the current user's local application-data directory, not beside the executable. The publish script builds in a clean staging directory and refuses to package an `EBWebView` or `*.WebView2` directory, preventing local browsing state or locked runtime files from entering the release archive.

## Residual risk

Complex graphics can consume CPU or memory even without active content. The XML character cap limits source size but is not a complete rendering-complexity limit. Users should avoid opening deliberately adversarial files from unknown sources, and future releases should consider configurable file-size and rendering-time limits.
