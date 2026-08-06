# AGENTS.md

## Project purpose and scope

SvgLiveEditor is an open-source Windows desktop application for editing UTF-8 SVG/XML source and viewing a live, security-restricted preview. Version 0.8.0 adds the host-side Layers & Groups foundation: a topmost-first visual tree, nested `<g>` containers, exact-span same-parent reorder, conservative source-backed visibility, and inherited session-only locks. It preserves the v0.7.1 editor UX, Arrange/opacity, resize handles, selection/movement, text measurement, installed-font picker, templates, recovery, validation-gated Auto Save, Inspector/Structure, preview, PNG sharing, file drop, and Pan functionality. Source text remains the single source of truth. It does not cover cross-parent reparenting, group creation/ungrouping, rotation, text resize handles, multi-selection, element creation/deletion/duplication, alignment, snapping, inline canvas text editing, complex text layout, path-node editing, explicit image/PDF file export, installers, updates, cloud features, full Figma-style editing, BPMN, or AI generation.

## Architecture overview

- `src/SvgLiveEditor`: one WPF application targeting `net10.0-windows`.
- `ViewModels`: window state, synchronized Layers/Structure/property state, session-only lock presentation, and document metadata.
- `Services`: testable SVG validation, validated source and visual-geometry indexing, host-side topmost-first Layers projection with opaque session identities, conservative visibility and inherited lock policy, browser-backed simple-text measurement, installed-font enumeration, coordinate mapping, conservative hit testing, bounded move/resize calculations, exact-span same-parent artwork/group ordering and layer-position presentation, conservative opacity analysis, minimal attribute edits, Source context-menu and Inspector Undo-shortcut policies, secure preview/PNG bridge generation, bounded clipboard/drag payloads, bounded UTF-8 file access, embedded template loading, recovery retention, safe path/atomic Auto Save policy, drag-file cleanup, debounce behavior, sample loading, and unsaved-change policy.
- `Models`: immutable results, source spans, indexed SVG nodes and visual geometry, text edits, and decisions shared by services and view models.
- `MainWindow`: WPF-only integration for AvalonEdit, Layers/Structure, Properties, dialogs, composition-mode WebView2, keyboard input, external file drop, internal Layers drag/drop, visual selection/movement/resizing, same-parent Arrange commands, opacity/visibility/lock gestures, and document persistence. Source-editor menu integration stays in `MainWindow.SourceEditor.cs`; Layers/Structure/property integration stays in `MainWindow.Inspector.cs`; visual-editing integration stays in `MainWindow.VisualEditing.cs`; template/recovery/Auto Save integration stays in `MainWindow.Persistence.cs`.
- `tests/SvgLiveEditor.Tests`: tests for security-sensitive and document logic that does not require launching WPF.
- `samples/welcome.svg`: the repository's original, safe starter SVG.
- `templates`: five original safe SVG resources embedded into the application; opening one creates a detached Modified document and never writes the resource.

Keep the application in one project until a concrete need justifies another production assembly.

## Important security requirements

- Treat every opened SVG/TXT file as untrusted.
- Treat `direction`, `unicode-bidi`, and `text-anchor` as passive presentation attributes only. Inspector edits for text/tspan must remain enum-constrained; do not expose bidi override values, rewrite text, insert hidden direction characters, or maintain preview-only direction state.
- Parse XML with `DtdProcessing.Prohibit`, a null `XmlResolver`, and bounded document size.
- Require an SVG root in the standard `http://www.w3.org/2000/svg` namespace.
- Reject scripts, inline event handlers, `foreignObject`, active embedded content, processing instructions, style elements, and non-fragment resource references before preview.
- Never insert raw SVG into host HTML. Preview only a Base64 data image under a restrictive CSP.
- Keep user SVG scripts, host objects, downloads, permissions, external navigation, pop-ups, and external resource requests disabled or blocked. If a fixed app-owned host interaction script is technically required, authorize only its exact CSP hash, keep the SVG isolated as a Base64 image, and accept only narrowly typed, strictly validated web messages.
- Visual hit testing must remain host-side over the current validated source index. Unsupported topmost artwork must not permit click-through: localize only bounds that can be derived conservatively, and otherwise fail closed. Keep visual messages exact-schema, navigation-token-bound, source-revision-bound, coordinate-bounded, and stale-safe; the app-owned overlay must contain only host-generated bounded geometry.
- Resize handles may appear only for current movable `rect`, `circle`, `ellipse`, and `line` geometry. Keep handle identifiers, opaque selection identities, random gesture IDs, physical pointer state, modifiers, source revisions, coordinates, and committed attribute edits strictly bounded and validated. Pointer movement may update only the host-owned temporary overlay; source changes once on a valid release and cancellation must never modify it.
- Arrange and Layers drag/drop may reorder only exact current full-source spans for eligible artwork or `<g>` siblings under the same indexed parent. Never cross parent/container boundaries, infer reparenting, or rewrite the whole document. Preview context-menu ordering must remain navigation-token-, source-revision-, and opaque-selection-bound and stale-safe.
- Layers visibility may add only a standard direct `display="none"` when `display`, `visibility`, inline style, and visibility animation ownership are unambiguous. Remove it only when the current session owns that hidden state; never overwrite authored visibility semantics. Locks are session-only, inherited through layer groups, never serialized into SVG/XML, and remain an editor guard rather than a security boundary; Source editing stays available.
- The Source context menu may invoke only AvalonEdit's existing text commands and Unicode text clipboard format. Preserve an existing selection when right-clicking inside it, and never route Source clipboard operations through WebView2 or SVG/HTML formats.
- The opacity control edits only a supported element's direct `opacity` presentation attribute with invariant values from 0 to 1. Keep slider movement UI-only until commit, preserve `fill-opacity` and `stroke-opacity`, remove optional `opacity` at 100%, and fail closed for style-owned opacity, malformed values, animation, transforms, or excluded visual effects.
- Text measurement may send only bounded host-validated direct text and constrained typography to the exact-hash trusted page. Keep requests and results token-, source-revision-, request-ID-, schema-, count-, and range-bound; never insert source markup, use `innerHTML`, expose the SVG DOM, or accept browser-supplied styling.
- Font suggestions may enumerate installed Windows families once per process and include generic fallbacks. The editable picker displays only the decoded primary family while the source-owned serialized stack remains available separately. Never download, embed, remotely load, or package fonts; preserve validated fallback entries when replacing the selected first family.
- Treat PNG bridge payloads as untrusted: enforce request, token, schema, dimension, pixel, encoded-length, decoded-length, MIME, PNG signature, and image-header checks before clipboard use.
- Direct preview drag must remain a paired, token-bound trusted-page gesture. Require an actual primary left-button pointer start on the isolated image, Pan-off arbitration, bounded coordinates, the Windows drag threshold, and a matching host-side armed gesture before requesting the existing validated PNG pipeline.
- Accept inbound drag data only from one local FileDrop `.svg` or `.txt` path. Reject text/HTML/URL payloads, remote paths, links, directories, unsupported extensions, and files above the documented 10,000,000-byte limit.
- Write drag-out PNGs only under the current user's scoped LocalAppData `SvgLiveEditor\DragOut` directory with random create-new names. Keep cleanup age, count, and total-size bounds; never derive a path from SVG source.
- Write recovery snapshots only under `%LocalAppData%\SvgLiveEditor\Recovery` with random application-generated IDs and atomic same-directory replacement. Keep the exact schema, source hash, size, age, count, and total-size checks; reject reparse points, ID/filename mismatches, malformed data, and unsafe stored paths. Restore source from the snapshot itself, never by following stored metadata.
- Auto Save is off by default and may write only an existing, supported, local, non-reparse, writable document that was manually opened or saved, including a safely reopened last document. Validate the exact captured source first, stage exact UTF-8 bytes beside the target, and recheck the document session/revision and path policy before atomic replacement. Never recreate a missing target or write an invalid revision.
- Do not weaken these rules without security tests and explicit rationale.
- Never add secrets, credentials, telemetry, or unexpected network access.

## Commands

Requires the .NET 10 SDK and Windows for the WPF build.

```powershell
dotnet restore SvgLiveEditor.sln
dotnet build SvgLiveEditor.sln --configuration Release --no-restore
dotnet test SvgLiveEditor.sln --configuration Release --no-build
dotnet run --project src/SvgLiveEditor/SvgLiveEditor.csproj
dotnet publish src/SvgLiveEditor/SvgLiveEditor.csproj --configuration Release --runtime win-x64 --self-contained true --property:PublishProfile=win-x64
./scripts/Publish-WinX64.ps1 -Version 0.8.0
```

The publish output is under `dist/win-x64/`. The publish script audits the self-contained win-x64 package and creates both `releases/SvgLiveEditor-vX.Y.Z-win-x64.zip` and its `.sha256` file locally. Packaged users do not need a separate .NET installation, but they must install Microsoft Edge WebView2 Evergreen Runtime and extract the full ZIP before running `SvgLiveEditor.exe`.

`.github/workflows/release.yml` runs for pushed stable `vX.Y.Z` tags and by manual dispatch for an existing tag. It validates and checks out the exact tag, confirms the project version, runs the deterministic non-desktop-integration tests, invokes the same audited packaging script, verifies the internal checksum, records it in logs and the job summary, and retains both files as a troubleshooting artifact. Only the ZIP is attached to new GitHub Releases; GitHub displays its public asset Digest. An existing Release's publication state, notes, and unrelated historical assets are not edited; a missing Release is created only as a draft with GitHub-generated notes, using the previous stable tag when available.

## Coding conventions

- Follow `.editorconfig`; use nullable reference types and implicit usings.
- Prefer plain C#, short methods, descriptive names, and early returns.
- Keep WPF-specific code in the view or view model; keep reusable logic free of UI dependencies.
- Use async work for XML validation and debounce so typing remains responsive.
- Preserve source text exactly on save; never reformat or normalize user XML.
- Add comments for security decisions and non-obvious behavior, not for syntax.
- Add focused tests for behavioral and security changes.

## Repository rules

- Preserve unrelated user changes and inspect `git status` before and after work.
- Do not add generated `bin`, `obj`, `dist`, publish, test-result, or local SDK folders.
- Samples and assets must be original or have documented redistribution permission. Do not copy third-party SVG, text, layouts, styles, coordinates, or archives.
- Keep package versions explicit and prefer stable releases.
- Do not create empty layers or speculative abstractions.
- Never commit, push, publish packages/artifacts, create or modify a remote repository, or create a GitHub Release without explicit user approval.
