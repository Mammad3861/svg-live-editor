# SvgLiveEditor

SvgLiveEditor is an open-source Windows desktop application for editing SVG/XML source and viewing the result immediately. Version 0.3.1 provides a document inspector on the left, source in the center, and a security-restricted preview on the right, with direct clipboard sharing, an app-owned preview context menu, and a discoverable Pan tool, while preserving the user's UTF-8 text exactly when saving.

Repository: [github.com/Mammad3861/svg-live-editor](https://github.com/Mammad3861/svg-live-editor)

## Screenshot

> Screenshot placeholder — an application screenshot will be added after the first UI review on Windows.

## Features

- Validated hierarchical SVG element tree with explicit tree-to-source navigation and non-destructive debounced caret-to-tree synchronization.
- Properties inspector for common safe attributes and shape geometry. Changes are minimal source edits, participate in Undo/Redo, and never reserialize the full XML document; path `d` is read-only in v0.3.
- AvalonEdit source editor with XML highlighting, line numbers, current-line highlighting, undo/redo, find/replace, and non-destructive word wrap. Toggle wrapping with `Alt+Z` or `Ctrl+Alt+W`.
- Automatic live preview after a 300 ms debounce, plus manual refresh.
- Preview zoom in/out, reset, and fit-to-area controls on a fixed checkerboard transparency canvas. The selected zoom mode is restored at the next launch; `Ctrl`+mouse wheel zooms around the pointer, normal wheel scrolling remains vertical, and `Shift`+wheel scrolls horizontally.
- A visible Pan toolbar mode (`H` to toggle and `Escape` to exit). Overflowing previews can also be panned temporarily with `Ctrl`+left drag, Space+left drag, or middle-button drag; ordinary left drag stays inert when Pan mode is off.
- Copy the complete valid artwork as a transparent PNG with **Preview > Copy Preview as PNG**, the **Copy Image** toolbar button, `Ctrl+Shift+C`, preview-focused `Ctrl+C`, or the fixed right-click preview menu, then paste directly into Telegram Desktop, Paint, Word, or another clipboard-aware application. The operation copies the full artwork rather than the scrolled viewport and does not change zoom or document state.
- Copy the editor's exact complete source with **Edit > Copy Entire SVG Source** or `Ctrl+Alt+C`. This preserves line endings, Persian text, invalid XML, caret, selection, and modified state; normal `Ctrl+C` keeps copying only selected text when the source editor or an Inspector property field has focus.
- New, Open, Save, Save As, Exit, and drag/drop for `.svg` and `.txt` files.
- The most recently opened or saved named document reopens by default at the next startup. The checkable View-menu preference and full path are stored only in the current user's LocalAppData settings; missing, inaccessible, or unsupported paths safely fall back to the welcome document.
- Unsaved-change prompts before replacing or closing a document.
- Strict UTF-8 reading and UTF-8-without-BOM writing without source reformatting.
- Secure XML validation with useful line and column errors.
- An original English/Persian welcome SVG included as the new-document template.
- An original multi-resolution application icon embedded in the executable, title bar, taskbar, and Alt+Tab presentation.

The trusted interaction bridge has automated browser integration coverage. Physical Ctrl+Wheel, the app-owned right-click menu, focus-sensitive Ctrl+C, clipboard paste compatibility, and the four drag-to-pan methods should still be confirmed on the target machine for each release.

The XML code editor intentionally remains left-to-right so markup punctuation and tag structure stay predictable. Persian text is preserved logically and as exact UTF-8, but mixed RTL/LTR caret movement and visual ordering are subject to AvalonEdit/WPF BiDi limitations; the saved source and SVG preview remain the authoritative checks.

## Security model and limitations

Opened files are untrusted. SvgLiveEditor prohibits DTDs, entity declarations, and external entity resolution; requires a standard SVG root; and rejects scripts, inline event handlers, `foreignObject`, active embedded content, processing instructions, style elements, navigation links, and non-fragment resources.

Validated SVG is UTF-8/Base64 encoded into an HTML `<img>` data URL. Raw SVG is never inserted into host markup. The host uses a restrictive Content Security Policy. One fixed, app-owned interaction script is authorized by its exact CSP SHA-256 hash for wheel, pan, fixed context-menu requests, focus-sensitive copy, and full-image PNG rendering. PNG copy draws only the already validated isolated image to a transparent off-screen canvas; it never exposes an SVG DOM. The trusted page and host exchange only exact-schema, per-navigation-token-bound messages. PNG responses are bounded and validated for request ID, MIME type, dimensions, Base64 length, PNG structure, signature, and IHDR size before clipboard use. Native browser/document zoom stays pinned to 100%, so only the SVG image dimensions change. User SVG scripts remain non-executable, and native host objects, arbitrary messages, permissions, downloads, pop-ups, external navigation, external requests, developer tools, and browser context menus are disabled or blocked. The only preview context menu is app-owned and contains Copy Preview as PNG, Fit, and Reset Zoom. Invalid edits keep the last valid preview visible.

Clipboard PNG output uses the SVG's intrinsic width/height, or its `viewBox` dimensions when intrinsic size is absent. Output preserves aspect ratio and is scaled down to at most 4096 pixels on either side and 8,000,000 total pixels (about 32 MB of uncompressed RGBA pixels). If the current source is invalid, Copy Preview intentionally copies the last valid image and says so; while validation is pending, it identifies the result as the last validated preview. If no valid visible preview exists, the command is unavailable or reports a non-destructive error.

The document inspector is built only from source accepted by the same secure validator. An explicit mouse or keyboard action in the tree reveals the corresponding source start tag; validation, reindexing, selection restoration, and caret-to-tree synchronization never change the editor selection. Supported property edits update the AvalonEdit document—the source remains the single source of truth—and then use the existing validation and preview pipeline. No SVG DOM is exposed to WebView2 or JavaScript.

This conservative MVP intentionally supports only a restricted safe SVG subset and does not render every valid SVG feature. In particular, external images/fonts/styles, embedded data resources, hyperlinks, `<style>`, `foreignObject`, scripts, and event handlers are rejected. Because exporter output varies, SvgLiveEditor is not guaranteed to open every SVG produced by tools such as Adobe Illustrator or Inkscape. The preview is not a general-purpose web browser or a replacement for reviewing SVG before distribution. See [docs/security-model.md](docs/security-model.md).

## Current scope

Version 0.3 provides tree/property-based source editing, clipboard PNG sharing, and preview navigation, not direct canvas manipulation. It does not support selecting, dragging, resizing, creating, deleting, or reordering SVG elements, and it does not provide freehand/path editing, PNG/PDF **file export**, an installer, or automatic updates.

## Roadmap

Future work may include improved AvalonEdit mixed RTL/BiDi caret and punctuation behavior; autosave through crash-recovery snapshots in LocalAppData (never continuous overwriting of the user's original file); built-in SVG templates; direct visual canvas selection and editing; element dragging and resizing; path editing; PNG/PDF file export; and an installer with automatic updates. These items are not part of v0.3.

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
./scripts/Publish-WinX64.ps1 -Version 0.3.1
```

This creates `releases/SvgLiveEditor-v0.3.1-win-x64.zip` and `releases/SvgLiveEditor-v0.3.1-win-x64.sha256`. Publishing is folder-based and intentionally not trimmed or forced into a single file, which is safer for WPF, WebView2 native dependencies, and startup reliability. This local command does not create or modify a GitHub Release.

## Automated GitHub Releases

The [release workflow](.github/workflows/release.yml) runs automatically when a stable semantic-version tag such as `v0.3.1` is pushed. It can also be started manually with an existing tag, which allows binary assets to be added to an existing Release. The workflow validates the tag, checks out its exact commit, confirms the project version, restores and builds in Release mode, runs tests excluding `TestCategory=DesktopIntegration`, and uses the same publish script to build and audit the package.

Both the ZIP and its `.sha256` file are retained as a GitHub Actions artifact and attached to the matching GitHub Release with safe replacement of identically named assets. An existing Release keeps its current publication state and manually edited notes; only matching binary assets are replaced. If no matching Release exists, the workflow creates a draft with GitHub-generated notes, using the previous stable tag when available, and leaves it unpublished for manual review.

## Application icon

The editable original is `assets/app-icon.svg`; the committed Windows icon is `src/SvgLiveEditor/Assets/SvgLiveEditor.ico`. The ICO embeds 16, 24, 32, 48, 64, 128, and 256 pixel frames and is compiled into the executable. To regenerate it deterministically on Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/Generate-AppIcon.ps1
```

The development-only generator uses Windows' built-in `System.Drawing`; it adds no application runtime dependency.

## Download

Download both `SvgLiveEditor-vX.Y.Z-win-x64.zip` and `SvgLiveEditor-vX.Y.Z-win-x64.sha256` from the matching GitHub Release. Use the attached `.sha256` file to verify the ZIP; no checksum is hardcoded in this documentation.

Extract the **entire** ZIP into a new folder before running `SvgLiveEditor.exe`. The package is self-contained for win-x64 and does not require a separate .NET installation. Microsoft Edge WebView2 Evergreen Runtime remains an external requirement and must be installed on the computer.

## Repository structure

```text
src/SvgLiveEditor/          WPF application, inspector, and secure editing services
tests/SvgLiveEditor.Tests/ Automated logic and security tests
samples/welcome.svg        Original safe starter document
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
