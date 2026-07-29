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

## Inbound file boundary

Open and window-wide drag/drop share the same strict UTF-8 reader and document-loading pipeline. File reads are bounded to 10,000,000 bytes before decoding, while XML parsing retains its independent ten-million-character bound. The drop adapter reads only the Windows `FileDrop` format and accepts exactly one fully qualified local `.svg` or `.txt` regular file. It rejects empty or multiple payloads, directories, `.lnk` shortcuts, reparse points, UNC/URL paths, unsupported extensions, missing files, unreadable metadata, and oversized files. Unicode text, HTML, browser URL, virtual-file, and arbitrary string payloads are never interpreted as paths.

The drop overlay is informational and non-hit-testable. A supported drop still runs the existing unsaved-change decision before reading or replacing the current document. Cancel leaves the document source, path, modified state, Inspector, and preview unchanged. A successfully read file is loaded as exact editable text even when its XML is invalid; normal validation then reports line/column information and the last valid safe preview remains visible.

## Document inspector boundary

The element tree is built only after `SvgValidationService` accepts the current source. The index stores source offsets and hierarchy; it does not expose a browser DOM and does not add IDs or metadata. Supported property changes replace one existing attribute value or insert one missing attribute in the selected start tag, then pass through the same validator and preview pipeline. The application never serializes the full XML document for an inspector edit, so unrelated whitespace, comments, attribute order, and UTF-8 text remain untouched.

## Local persistence boundary

Crash-recovery source is written only beneath the current user's fixed `%LocalAppData%\SvgLiveEditor\Recovery` directory. Snapshot names use 128-bit random application-generated identifiers and are never derived from a document path, display name, or source. Writes use create-new temporary files and atomic same-directory replacement. The loader accepts only the current exact JSON schema and validates identifier syntax, identifier/filename agreement, metadata, normalized supported original paths, the 10,000,000-byte UTF-8 source limit, and an exact SHA-256 of the source. Recovery files and their immediate parent directory must not be reparse points. Malformed, oversized, expired, tampered, or mismatched records are rejected and removed. Retention is bounded to seven days, ten snapshots, and 100,000,000 bytes.

The stored original path is metadata, not a source location. Recovery always restores the exact source contained in the validated snapshot. A path is retained only as an in-memory document association when the existing local SVG/TXT file passes the safe read policy; otherwise the snapshot opens as recovered untitled work. A byte-identical snapshot is removed without prompting. Restore never writes the original, Discard deletes only the selected managed snapshot, and Skip retains it for a later launch.

Auto Save is a separate opt-in pipeline and is disabled by default. It applies only to an existing named `.svg` or `.txt` document that was manually opened or saved, including a safely reopened last document. The exact captured revision must pass `SvgValidationService`; invalid XML pauses automatic writes while recovery continues. Network paths, missing files, directories, read-only files, unsupported drives, inaccessible paths, file or directory reparse points, and policy failures are rejected. Exact UTF-8 bytes are staged to a random create-new temporary file in the target directory. Immediately before atomic replacement, the host rechecks the target policy and confirms that the document session, revision, path, and full source still match. A stale delayed write is disposed and cannot overwrite a newer edit. Manual Save and Save As retain their existing behavior.

## Rendering boundary

Validated source is encoded as UTF-8 and then Base64. A trusted, fixed HTML host loads it through an `<img src="data:image/svg+xml;base64,...">`; raw source is never concatenated into markup or an iframe `srcdoc`. Image-mode SVG does not provide an interactive document scripting surface.

The host Content Security Policy defaults all sources to none and permits only the Base64 data image, fixed inline host CSS, and one fixed app-owned interaction script authorized by its exact SHA-256 hash. The same LF-normalized script bytes are used both for hashing and insertion. The script handles preview wheel scrolling, pointer panning, focus-sensitive copy requests, fixed context-menu requests, and bounded full-image PNG rendering. It does not receive or insert SVG source, expose an SVG DOM, call `eval`, load code, navigate, access files, or make network requests. Connections, fonts, media, objects, forms, and base URL changes remain disabled.

Copy Preview, direct artwork drag, and Drag Image render only the already validated `<img>` into a transparent, app-owned off-screen canvas at dimensions selected by the host. They do not capture the viewport, checkerboard, scrollbars, window chrome, editor, or Inspector. The requested output preserves aspect ratio and is bounded to 4096 pixels per side and 8,000,000 pixels total. Oversized SVG canvases are scaled down before a browser canvas or host bitmap is allocated. Rendering does not navigate or modify source, preview zoom, or scroll state.

The host revalidates the returned PNG before clipboard or drag use. Direct artwork drag and Drag Image then write only those validated PNG bytes to a random create-new filename under `%LocalAppData%\SvgLiveEditor\DragOut` and publish Windows FileDrop, PNG, and Bitmap data formats. The filename is never derived from SVG source and the DataObject contains no SVG/XML. Cancelled drags are deleted when safe. Successful files are retained for asynchronous drop consumers and cleaned at startup, before creation, and every six hours. Managed files older than 24 hours are removed; the store is bounded to 20 files and 200,000,000 bytes. Cleanup errors are contained and cannot crash startup.

## WebView2 boundary

WebView2 enables scripting only so the CSP-hashed trusted host interaction script can run. The SVG remains a Base64 image rather than a DOM document, so SVG scripts cannot execute in the host context. Web messages use exact command-specific schemas and the unpredictable per-navigation token. The application rejects malformed messages, unknown commands, unexpected fields, stale tokens, unexpected sources, out-of-range values, and unsupported directions. `NavigateToString` gives the trusted page an opaque `about:blank` message origin, so source validation is paired with that token rather than trusting the origin alone.

SVG bidi presentation remains source-owned and passive. The Inspector exposes only constrained `direction`, `unicode-bidi`, and `text-anchor` values for text/tspan elements; directional override values are not offered. These edits use the same stale-span checks, single source edit, strict SVG revalidation, and one-operation Undo path as other properties. The preview does not infer language, rewrite text, add invisible bidi controls, or create hidden direction state.

Host-to-page messages can only update the existing image's zoom/normalized viewport, set Pan mode and the bounded Windows drag thresholds, or request a bounded PNG from the existing image. Page-to-host messages can only report normalized viewport state, request a bounded zoom/Pan command, request the fixed app-owned context menu at bounded viewport coordinates, request focus-sensitive copy, perform the paired direct-drag arm/start/cancel handshake, return a fixed PNG error, or return one matching PNG response. Direct drag is armed only by a trusted primary mouse pointer that begins on the isolated image while Pan and its modifiers are inactive. The host independently validates the `about:blank` source, current navigation token, exact schema, random gesture ID, bounded coordinates, system-distance threshold, current Pan state, and physical left-button state. Focus loss, navigation, cancellation, or pointer termination resets the handshake. A PNG response must match the pending navigation token and random request ID and have the exact schema, `image/png` MIME type, requested dimensions, bounded Base64 and decoded lengths, PNG signature, valid chunk framing, one matching IHDR, image data, and a terminal IEND before it reaches the Windows clipboard decoder. Only one PNG request can be pending, and navigation cancels it. No arbitrary command dispatch or host object is available.

Script dialogs, host objects, permissions, downloads, pop-ups, external navigation, browser context menus, developer tools, pinch zoom, password saving, and autofill remain disabled. Right-click is canceled inside the trusted page and can open only the WPF-owned menu containing Copy Preview as PNG, Fit, and Reset Zoom; it cannot expose browser navigation, printing, page saving, inspection, or external-link commands. WebView2's Ctrl+Wheel dispatch is enabled because disabling its zoom controls suppresses the physical wheel event before the trusted page can receive it. The page captures Ctrl+Wheel with a non-passive listener, prevents native document zoom, and asks the host to resize only the SVG image; a host-side guard keeps the WebView zoom factor at 100%. Resource interception permits only the initial blank document, the generated `data:text/html` host, and the `data:image/svg+xml;base64` image, and returns a blocked response for everything else. No host object is exposed to SVG content.

The WPF preview uses `WebView2CompositionControl` so Windows drag input remains in the WPF visual/input tree rather than being captured by a separate browser child HWND. WebView2 external drop handling is disabled, so files are never navigated or opened by the trusted page. The composition control changes input routing only; it uses the same CoreWebView2 environment, CSP-hashed page, navigation policy, message validation, resource interception, and 1.0 document zoom guard.

The MVP uses the installed Evergreen WebView2 Runtime. A self-contained .NET publish does not bundle that runtime.

WebView2 profile and cache data is stored under the current user's local application-data directory, not beside the executable. The publish script builds in a clean staging directory and refuses to package an `EBWebView` or `*.WebView2` directory, preventing local browsing state or locked runtime files from entering the release archive.

## Residual risk

Complex graphics can consume CPU or memory even without active content. The XML character cap and PNG output caps limit source and bitmap size but are not complete rendering-complexity or rendering-time limits. PNG structural validation does not independently reimplement every decoder check; WPF's image decoder performs the final decode after the bridge-specific bounds and structure checks. Users should avoid opening deliberately adversarial files from unknown sources, and future releases should consider configurable file-size and rendering-time limits.
