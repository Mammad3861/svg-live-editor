# SvgLiveEditor

SvgLiveEditor is an open-source Windows desktop application for editing SVG/XML source and viewing the result immediately. Version 0.9.0 Visual Authoring is a complete standalone release that adds safe element creation, duplication, deletion, and explicit conservative cross-parent layer moves on top of the shipped v0.8 Layers & Groups workspace. The exact UTF-8 SVG source remains the single source of truth.

Repository: [github.com/Mammad3861/svg-live-editor](https://github.com/Mammad3861/svg-live-editor)

## Screenshot

> Screenshot placeholder — an application screenshot will be added after the first UI review on Windows.

## Features

- Native **Layers** and **Structure** tabs share one validated source index. Layers shows visual artwork and nested `<g>` containers with the frontmost item first while filtering definitions such as gradients, markers, masks, filters, and metadata; Structure preserves the complete XML hierarchy. Selection stays synchronized with Preview, Properties, Source/caret, and both trees, and selecting a Layers row does not unexpectedly replace the Source selection.
- The compact **+ Add** control and the Layers/Structure context menus create bounded `rect`, `circle`, `ellipse`, `line`, `text`, and empty `g` elements without raw XML input. New geometry is derived from the current safe canvas/viewBox, inserted at the front of the selected eligible group or sibling context, selected immediately in both trees and Properties, and committed as one Undo operation. `Ctrl+D` duplicates the selected layer when Layers, Structure, or Preview has focus; Delete removes an eligible selection, with confirmation for non-empty groups. Neither shortcut takes over Source or property-field text editing.
- Properties inspector for common safe attributes, shape geometry, and text typography, with visible focusable info buttons, concise tooltips, and screen-reader help for common SVG properties. The editable `font-family` picker shows only the clean, unquoted primary family, while its tooltip and the SVG source retain the complete validated fallback stack. Choosing or typing a primary family replaces only that entry and safely quotes names when required. Committed changes are minimal source edits, remain undoable with `Ctrl+Z`/`Ctrl+Y` while focus stays in Properties, and never reserialize the full XML document; uncommitted field typing keeps standard local text-box Undo/Redo and path `d` remains read-only.
- AvalonEdit source editor with XML highlighting, line numbers, current-line highlighting, undo/redo, find/replace, non-destructive word wrap, and a standard app-owned right-click menu for Undo, Redo, Cut, Copy, Paste, Delete, and Select All. Right-clicking selected source preserves the selection. Toggle wrapping with `Alt+Z` or `Ctrl+Alt+W`.
- A keyboard-accessible native WPF template gallery under **File > New from Template** (`Ctrl+Alt+N`) with Blank Canvas, App Icon, Social Card, Flow Diagram, and Persian/RTL starters. Templates pass the same strict SVG validator and open as detached Modified documents that require Save As; the embedded resource is never overwritten. Text placeholders carry explicit passive bidi metadata: the Persian starter uses `direction="rtl"`, `unicode-bidi="embed"`, and RTL-correct `text-anchor="start"`, while neutral placeholders use `unicode-bidi="plaintext"` without directional overrides.
- Automatic live preview after a 300 ms debounce, plus manual refresh.
- Preview zoom in/out, reset, and fit-to-area controls on a fixed checkerboard transparency canvas. The selected zoom mode is restored at the next launch; `Ctrl`+mouse wheel zooms around the pointer, normal wheel scrolling remains vertical, and `Shift`+wheel scrolls horizontally.
- A visible Select tool (`V`) synchronizes Live Preview selection with the Document Inspector, Properties, and the selected source start tag. `rect`, `circle`, `ellipse`, `line`, and conservatively supported direct `<text>` can be dragged or nudged with Arrow keys (1 SVG unit) and `Shift`+Arrow (10 SVG units). Eligible shapes show fixed-screen-size white, accent-bordered host-owned resize handles and a thin accessible selection outline; hover, selected, moving, and resizing states remain distinct at Fit or manual zoom. Rectangles and ellipses have four corner and four edge handles, circles have four cardinal radius handles, and lines have endpoint handles. Shift preserves the original aspect ratio for rectangle and ellipse corner drags. A move or resize displays an app-owned temporary outline and commits exactly one Undo operation on release; source is never rewritten on every pointer move.
- **Arrange** provides Bring to Front, Bring Forward, Send Backward, and Send to Back for eligible artwork and `<g>` units under the same parent. The shortcuts are `Ctrl+Shift+]`, `Ctrl+]`, `Ctrl+[`, and `Ctrl+Shift+[`. These commands never reparent. Layers drag/drop now has distinct before, after, and inside-group feedback: edge drops retain ordering semantics, while an explicit cross-parent drop can move a layer into an existing group, out to the root beside a root layer, or between groups. A keyboard-accessible **Move to SVG Root (Front)** command covers the common move-out route. Cross-parent moves are rejected when inherited transforms, styles, opacity, clipping, masks, filters, fonts, visibility, namespace state, locks, stale identities, or other context semantics make appearance preservation ambiguous.
- Each Layers row has app-owned visibility and lock controls. Visibility toggling adds only a standard `display="none"` to an otherwise unambiguous element and removes it only while that hidden attribute is known to belong to the current editor session. Existing authored `display`, `visibility`, inline style, or animation ownership is never overwritten; affected controls are disabled with an explanation. Hidden state remains in the SVG and therefore survives Save/reopen. Locks are deliberately session-only because SVG has no portable editor-lock attribute: an inherited group lock blocks Create into the group, Duplicate, Delete, Move, Resize, Nudge, Arrange, reorder/reparent, Opacity, and supported Properties edits, but Source editing remains available.
- Properties shows a dedicated element **Opacity** slider and percentage field for eligible artwork. Slider movement is candidate-only and commits one source edit on release; typing commits with Enter or focus loss, and Escape reverts. Missing opacity is 100%, and committing 100% removes the optional `opacity` attribute without changing `fill-opacity` or `stroke-opacity`. Style-owned opacity, malformed values, animation, transforms, and excluded effects disable the control conservatively.
- A visible Pan toolbar mode (`H` to toggle and `Escape` to exit). Overflowing previews can also be panned temporarily with `Ctrl`+left drag, Space+left drag, or middle-button drag. Pan mode and these alternate gestures always take priority over image sharing.
- Copy the complete valid artwork as a transparent PNG with **Preview > Copy Preview as PNG**, the **Copy Image** toolbar button, `Ctrl+Shift+C`, preview-focused `Ctrl+C`, or the fixed right-click preview menu, then paste directly into Telegram Desktop, Paint, Word, or another clipboard-aware application. The operation copies the full artwork rather than the scrolled viewport and does not change zoom or document state.
- Use `Alt`+left-drag on the rendered artwork to drag a PNG into Explorer, Telegram Desktop, Paint, or another Windows drop target. Plain primary drag is reserved for Select; starting on checkerboard space, clicking without crossing the Windows drag threshold, or using a Pan gesture does not export. The visible **Drag Image** toolbar control remains an accessible fallback. Both entry points use the same validated full-artwork render and provide a real PNG file plus PNG/bitmap formats while preserving transparency.
- Copy the editor's exact complete source with **Edit > Copy Entire SVG Source** or `Ctrl+Alt+C`. This preserves line endings, Persian text, invalid XML, caret, selection, and modified state; normal `Ctrl+C` keeps copying only selected text when the source editor or an Inspector property field has focus.
- New, Open, Save, Save As, Exit, and window-wide file drag/drop for one local `.svg` or `.txt` file. A **Drop SVG or TXT to open** overlay works over the Source editor, Inspector, Properties, and Live Preview. The same unsaved-change prompt runs before replacement; Cancel leaves the current document unchanged. Files are read as strict UTF-8 and bounded to 10,000,000 bytes.
- The most recently opened or saved named document reopens by default at the next startup. The checkable View-menu preference and full path are stored only in the current user's LocalAppData settings; missing, inaccessible, or unsupported paths safely fall back to the welcome document.
- Crash recovery is enabled by default. Exact UTF-8 source—including invalid XML and Persian text—is debounced into atomic, random-ID snapshots under `%LocalAppData%\SvgLiveEditor\Recovery`. Startup offers Restore, Discard, or Skip before reopening the last document. Restore opens the snapshot in memory as Modified and never overwrites the original; redundant snapshots identical to the original are removed without prompting. Snapshots expire after 7 days and are bounded to 10 files and 100,000,000 bytes.
- Optional **File > Auto Save** is off by default and persisted per user. For an existing named `.svg` or `.txt` document that was manually opened or saved, it atomically writes the exact current UTF-8 revision two seconds after editing only when the strict SVG validator accepts it. Invalid XML, a missing/read-only/inaccessible original, reparse points, network paths, unsupported drives, or policy failures pause Auto Save without touching the original; recovery snapshots continue. Untitled and template documents still require Save As.
- Unsaved-change prompts before replacing or closing a document.
- Strict UTF-8 reading and UTF-8-without-BOM writing without source reformatting.
- Secure XML validation with useful line and column errors.
- An original English/Persian welcome SVG included as the new-document template.
- An original multi-resolution application icon embedded in the executable, title bar, taskbar, and Alt+Tab presentation.

The trusted interaction bridge has automated browser integration coverage, including Chromium metrics for English and Persian text, a real pointer-driven rectangle resize, and revision/selection-bound context-menu requests. Physical Add/Duplicate/Delete, before/after/inside Layers drops, move-to-root, selection/expansion restoration, eye and lock controls, shape/text selection, movement and resize handles, Arrange ordering, opacity gestures, font changes, inbound drops, `Alt`+artwork drag and Drag Image compatibility, Ctrl+Wheel, the app-owned right-click menu, focus-sensitive Ctrl+C, clipboard paste compatibility, touchpad scrolling, and the four drag-to-pan methods should still be confirmed on the target machine for each release.

The XML code editor intentionally remains left-to-right so markup punctuation and tag structure stay predictable. Persian text and punctuation are preserved in exact logical UTF-8 order, but mixed RTL/LTR caret movement and source-editor glyph placement remain subject to AvalonEdit/WPF BiDi limitations. The SVG preview renders the source exactly as authored: explicit `direction`, `unicode-bidi`, and `text-anchor` attributes control SVG text layout, while text without such metadata keeps the viewer's normal SVG bidi behavior. SvgLiveEditor never rewrites Persian text or inserts hidden direction characters.

## Security model and limitations

Opened files are untrusted. SvgLiveEditor prohibits DTDs, entity declarations, and external entity resolution; requires a standard SVG root; and rejects scripts, inline event handlers, `foreignObject`, active embedded content, processing instructions, style elements, navigation links, and non-fragment resources.

Validated SVG is UTF-8/Base64 encoded into an HTML `<img>` data URL. Raw SVG is never inserted into host markup. The host uses a restrictive Content Security Policy. One fixed, app-owned interaction script is authorized by its exact CSP SHA-256 hash for wheel, pan, bounded visual move/resize pointer requests, app-owned selection overlays and handles, fixed context-menu requests, focus-sensitive copy, bounded Chromium text measurement, and full-image PNG rendering. Resize requests are bound to the navigation token, current source revision, opaque selection identity, known handle, random gesture ID, bounded coordinates, modifiers, and physical primary-button state. Preview Arrange requests are additionally accepted only while the context-menu navigation token, source revision, opaque selection ID, and host selection are all current. Text measurement receives only host-validated plain text and constrained typography—not SVG markup—and returns finite bounds through an exact token/revision/request schema. PNG copy draws only the already validated isolated image to a transparent off-screen canvas, so selection overlays and handles are never copied; neither path exposes an SVG DOM. Visual hit testing and geometry edits run in .NET against the validated source index, not an inline SVG DOM. Native browser/document zoom stays pinned to 100%, so only the SVG image and app-owned overlay dimensions change. User SVG scripts remain non-executable, and native host objects, arbitrary messages, permissions, downloads, pop-ups, external navigation, external requests, developer tools, and browser context menus are disabled or blocked. The only preview context menu is app-owned and contains Copy Preview as PNG, Fit, Reset Zoom, and the four bounded Arrange commands. Invalid edits keep the last valid preview visible and disable visual editing.

Clipboard and Drag Image PNG output use the SVG's intrinsic width/height, or its `viewBox` dimensions when intrinsic size is absent. Output preserves aspect ratio and is scaled down to at most 4096 pixels on either side and 8,000,000 total pixels (about 32 MB of uncompressed RGBA pixels). If the current source is invalid, sharing intentionally uses the displayed last valid image and says so; while validation is pending, it identifies the result as the last validated preview. If no valid visible preview exists, sharing is unavailable or reports a non-destructive error.

For interoperable Windows drag-out, a validated PNG is written with a random application-owned name under `%LocalAppData%\SvgLiveEditor\DragOut`. SVG source is never written beside it. Cancelled drags are deleted when safe. Successful drag files remain available for applications that read asynchronously, then cleanup runs at startup, before another drag image is created, and every six hours. Managed files older than 24 hours are removed, and the directory is bounded to 20 files and 200,000,000 bytes. Cleanup failures do not prevent application startup.

Recovery persistence is separate from preview rendering. Snapshots are written only to the fixed current-user `%LocalAppData%\SvgLiveEditor\Recovery` directory with random application-generated identifiers and atomic same-directory replacement. The loader accepts only the exact versioned JSON schema, validates ID/filename agreement, metadata, normalized supported paths, source size, and SHA-256, rejects reparse points and malformed or tampered records, and never follows a stored path to restore source. An original path is used only as a safe existing document association; the snapshot source remains authoritative. Auto Save stages exact UTF-8 bytes beside an eligible existing local file, rechecks the path policy and exact document session/revision before atomic replacement, and never recreates a missing original.

Layers and Structure are built only from source accepted by the same secure validator. Both are WPF/host-side projections of exact indexed spans; no new WebView message or SVG DOM access is involved. Mouse selection in Layers updates the current element without replacing Source selection, while explicit keyboard navigation can reveal the matching start tag. Creation accepts only six app-owned element kinds and bounded host-derived defaults. Duplicate remaps deterministic unique IDs and analyzed local `href`/`url(#id)` references inside the copied subtree, or fails closed when a target is missing or ambiguous. Delete and cross-parent moves recheck current spans, locks, ancestry, definition boundaries, source revision, and semantic context before applying one minimal edit. Cross-parent moves are deliberately rejected when inherited presentation or namespace behavior could change. Supported authoring, property, opacity, visibility, Arrange, Layers reorder, and visual edits update the AvalonEdit document—the source remains the single source of truth—and then use the existing validation, Auto Save, Recovery, and preview pipeline. For `<text>`, Properties exposes `x`, `y`, `font-family`, `font-size`, `font-weight`, `font-style`, `fill`, `direction`, `unicode-bidi`, and `text-anchor`, plus the dedicated opacity percentage control. The picker displays the decoded primary family only; its tooltip shows the complete serialized source stack. Choosing an installed font commits immediately, replaces only the primary family, and retains safe local fallbacks; a typed primary commits with Enter or focus loss, while Escape restores the current source value. Fonts are never downloaded, embedded, or packaged. Constrained bidi choices do not include override values. No SVG DOM is exposed to WebView2 or JavaScript.

This conservative MVP intentionally supports only a restricted safe SVG subset and does not render every valid SVG feature. In particular, external images/fonts/styles, embedded data resources, hyperlinks, `<style>`, `foreignObject`, scripts, and event handlers are rejected. Because exporter output varies, SvgLiveEditor is not guaranteed to open every SVG produced by tools such as Adobe Illustrator or Inkscape. The preview is not a general-purpose web browser or a replacement for reviewing SVG before distribution. See [docs/security-model.md](docs/security-model.md).

## Current scope

Version 0.9.0 Visual Authoring is a complete standalone release containing safe creation of `rect`, `circle`, `ellipse`, `line`, direct `text`, and empty `g`; adjacent duplication with deterministic ID/reference remapping; exact-subtree deletion; and explicit conservative reparenting into, out of, and between existing groups. Validated, untransformed `rect`, `circle`, `ellipse`, and `line` elements with unitless or `px` geometry remain selectable, movable, and resizable; simple direct `<text>` remains selectable and movable, and conservatively bounded `path`, `polygon`, and `polyline` remain inspection-only. Multi-selection, moving multiple selected elements, Group/Ungroup, alignment/distribution, basic snapping, and evaluation of safe basic path bounding-box resize are planned separately for v0.10.0 Visual Composition. Rotation, text resize handles, inline canvas text editing, and path-node editing remain later work. Unsafe or ambiguous operations fail closed. Explicit PNG/PDF **file export**, an installer, and automatic updates are also not provided.

Precise before/after/inside cross-parent placement currently uses the Layers drag surface. **Move to SVG Root (Front)** provides a keyboard-accessible move-out route; a keyboard target chooser for moving into or between groups remains a future accessibility improvement.

| Preview/Layers category | v0.9.0 behavior |
| --- | --- |
| `rect`, `circle`, `ellipse`, `line` | Selectable, movable, and resizable when geometry and ancestors meet the conservative unit/transform/effect rules. Rectangles and ellipses expose eight handles, circles four cardinal handles, and lines two endpoint handles. |
| Direct simple `<text>` | Selectable and movable after trusted bounded text measurement. Text size is edited with the `font-size` field in Properties using a positive unitless or `px` value. |
| `path`, `polygon`, `polyline` | Never movable. Selectable for inspection when safe conservative bounds are available; otherwise a concise fail-closed reason is shown. Topmost unsupported artwork never clicks through to lower content. |
| `g` | Existing and newly created empty groups are expandable in Layers and Structure, reorderable as one same-parent paint-order unit, valid explicit reparent targets, and able to carry conservative visibility/session-lock state. Group/ungroup commands are not included. Preview still selects eligible children rather than exposing an SVG group DOM. |
| `tspan`, `textPath`, nested/complex text | Not visually movable; remains editable through Source and Inspector. |
| Transformed, clipped, masked, filtered, animated, hidden, malformed, or unsupported-unit content | Not visually movable; ambiguous topmost content is never bypassed to select a lower element. |
| `defs`, gradients, markers, patterns, and definition content | Not treated as independently visible selectable artwork. |

Text resize handles are deliberately not part of v0.9.0; use the validated `font-size` property instead. The minimum shape dimension is 0.01 SVG user units, and crossing an anchored edge clamps at that value rather than flipping the element.

## Roadmap

The [official roadmap](docs/roadmap.md) separates shipped work, pre-v1 essentials, and optional post-v1 ideas. It is a planning guide, not a promise or fixed delivery schedule. Auto Save remains deliberately validation-gated and is not a replacement for version control or backups.

## Requirements

To build:

- Windows 10 or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`10.0.302` or a compatible newer .NET 10 feature band)

To run:

- Windows 10 or Windows 11
- [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/#download-section)

The self-contained Windows publish includes the .NET Desktop Runtime, so users of the packaged ZIP do not need to install the .NET Desktop Runtime separately. It does **not** include WebView2 Evergreen Runtime.

Pinned stable NuGet versions: AvalonEdit `6.3.1.120`, Microsoft.Web.WebView2 `1.0.4078.44`, MSTest `4.3.2`, and Microsoft.NET.Test.Sdk `18.8.1`.

## Build, test, and run

```powershell
dotnet restore SvgLiveEditor.sln
dotnet build SvgLiveEditor.sln --configuration Release --no-restore
dotnet test SvgLiveEditor.sln --configuration Release --no-build
dotnet run --project src/SvgLiveEditor/SvgLiveEditor.csproj
```

## Publish for win-x64

Create the self-contained output:

```powershell
dotnet publish src/SvgLiveEditor/SvgLiveEditor.csproj --configuration Release --runtime win-x64 --self-contained true --property:PublishProfile=win-x64
```

Output is written to `dist/win-x64`. To publish, audit, and create versioned ZIP and SHA-256 files locally:

```powershell
./scripts/Publish-WinX64.ps1 -Version 0.9.0
```

This creates `releases/SvgLiveEditor-v0.9.0-win-x64.zip` and the internal/local `releases/SvgLiveEditor-v0.9.0-win-x64.sha256`. Publishing is folder-based and intentionally not trimmed, ReadyToRun-enabled, or forced into a single file, which is safer for WPF, WebView2 native dependencies, startup reliability, and the established package-size baseline. This local command does not create or modify a GitHub Release.

## Automated GitHub Releases

The [release workflow](.github/workflows/release.yml) runs automatically when a stable semantic-version tag such as `v0.9.0` is pushed. It can also be started manually with an existing tag, which allows binary assets to be added to an existing Release. The workflow validates the tag, checks out its exact commit, confirms the project version, restores and builds in Release mode, runs tests excluding `TestCategory=DesktopIntegration`, and uses the same publish script to build and audit the package.

The ZIP and its internal `.sha256` file are retained as a short-lived GitHub Actions troubleshooting artifact, but only the ZIP is attached to new GitHub Releases. The workflow verifies the internal checksum before upload and records the SHA-256 in its logs and job summary. An existing Release keeps its publication state, manually edited notes, and unrelated historical assets; only the matching ZIP is replaced. If no matching Release exists, the workflow creates a draft with GitHub-generated notes, using the previous stable tag when available, and leaves it unpublished for manual review.

## Application icon

The editable original is `assets/app-icon.svg`; the committed Windows icon is `src/SvgLiveEditor/Assets/SvgLiveEditor.ico`. The ICO embeds 16, 24, 32, 48, 64, 128, and 256 pixel frames and is compiled into the executable. To regenerate it deterministically on Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/Generate-AppIcon.ps1
```

The development-only generator uses Windows' built-in `System.Drawing`; it adds no application runtime dependency.

## Download

Download `SvgLiveEditor-vX.Y.Z-win-x64.zip` from the matching GitHub Release. GitHub displays the expected **Digest** directly under the ZIP asset. Calculate the downloaded file's SHA-256 in PowerShell and compare it with that displayed digest:

```powershell
Get-FileHash .\SvgLiveEditor-v0.9.0-win-x64.zip -Algorithm SHA256
```

Extract the **entire** ZIP into a new folder before running `SvgLiveEditor.exe`. The package is self-contained for win-x64 and does not require a separate .NET installation. Microsoft Edge WebView2 Evergreen Runtime remains an external requirement and must be installed on the computer.

## Repository structure

```text
src/SvgLiveEditor/          WPF application, inspector, and secure editing services
tests/SvgLiveEditor.Tests/ Automated logic and security tests
samples/welcome.svg        Original safe starter document
templates/                 Five original embedded safe SVG templates
assets/                    Original editable artwork
docs/                      Architecture and security notes
.github/workflows/         Windows build, test, and release workflows
scripts/                   Local publish helper
```

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting a change. Only contribute samples and assets you created yourself or have explicit permission to redistribute. Do not add copied, copyrighted, or ambiguously licensed SVG files, layouts, text, coordinates, or archives.

When reporting a bug, include the version and Windows architecture shown under **Help > About SvgLiveEditor**, together with the steps needed to reproduce the problem.

## Reporting security problems

Do not disclose an exploitable security problem in a public issue before it can be assessed. Follow the private-reporting guidance in [CONTRIBUTING.md](CONTRIBUTING.md#security-issues).

## License

Code created for this repository is available under the [MIT License](LICENSE).
