# AGENTS.md

## Project purpose and scope

SvgLiveEditor is an open-source Windows desktop application for editing UTF-8 SVG/XML source and viewing a live, security-restricted preview. The MVP covers source editing, file operations, validation, preview zoom/fit, and local publishing. It does not cover image/PDF export, installers, updates, cloud features, visual SVG editing, BPMN, or AI generation.

## Architecture overview

- `src/SvgLiveEditor`: one WPF application targeting `net10.0-windows`.
- `ViewModels`: window state and presentation-friendly document metadata.
- `Services`: testable SVG validation, secure preview HTML generation, UTF-8 file access, debounce behavior, sample loading, and unsaved-change policy.
- `Models`: small immutable results and decisions shared by services and the view model.
- `MainWindow`: WPF-only integration for AvalonEdit, dialogs, WebView2, keyboard input, and drag/drop.
- `tests/SvgLiveEditor.Tests`: tests for security-sensitive and document logic that does not require launching WPF.
- `samples/welcome.svg`: the repository's original, safe starter SVG.

Keep the application in one project until a concrete need justifies another production assembly.

## Important security requirements

- Treat every opened SVG/TXT file as untrusted.
- Parse XML with `DtdProcessing.Prohibit`, a null `XmlResolver`, and bounded document size.
- Require an SVG root in the standard `http://www.w3.org/2000/svg` namespace.
- Reject scripts, inline event handlers, `foreignObject`, active embedded content, processing instructions, style elements, and non-fragment resource references before preview.
- Never insert raw SVG into host HTML. Preview only a Base64 data image under a restrictive CSP.
- Keep user SVG scripts, host objects, downloads, permissions, external navigation, pop-ups, and external resource requests disabled or blocked. If a fixed app-owned host interaction script is technically required, authorize only its exact CSP hash, keep the SVG isolated as a Base64 image, and accept only narrowly typed, strictly validated web messages.
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
./scripts/Publish-WinX64.ps1 -Version 0.1.0
```

The publish output is under `dist/win-x64/`. The publish script audits the self-contained win-x64 package and creates both `releases/SvgLiveEditor-vX.Y.Z-win-x64.zip` and its `.sha256` file locally. Packaged users do not need a separate .NET installation, but they must install Microsoft Edge WebView2 Evergreen Runtime and extract the full ZIP before running `SvgLiveEditor.exe`.

`.github/workflows/release.yml` runs for pushed stable `vX.Y.Z` tags and by manual dispatch for an existing tag. It validates and checks out the exact tag, confirms the project version, runs the deterministic non-desktop-integration tests, invokes the same audited packaging script, uploads both files as a workflow artifact, and attaches them to the matching GitHub Release. Identically named assets are replaced; a missing Release is created only as a draft.

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
