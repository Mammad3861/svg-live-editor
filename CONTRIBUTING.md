# Contributing to SvgLiveEditor

Thanks for helping improve SvgLiveEditor.

## Set up

1. Use Windows 10/11 with the .NET 10 SDK and WebView2 Evergreen Runtime.
2. Fork or clone the repository and create a focused branch, for example `feature/better-find` or `fix/preview-validation`.
3. Run `dotnet restore SvgLiveEditor.sln` and `dotnet build SvgLiveEditor.sln --configuration Release`.

Keep changes small, readable, and aligned with the simple MVVM-based structure. Avoid adding layers until a concrete requirement needs them.

## Test changes

Run before submitting:

```powershell
dotnet test SvgLiveEditor.sln --configuration Release
```

Add behavioral tests for validation, preview security, file preservation, and document decisions. Manually check relevant WPF behavior on Windows when the UI changes.

## Submit changes

- Use a focused branch; do not work directly on `main` in a shared repository.
- Explain the user-visible behavior, security impact, and test evidence in the pull request.
- Do not include generated output from `bin`, `obj`, `dist`, `releases`, or `TestResults`.
- Do not add dependencies without explaining why the existing platform cannot meet the need.

## Security issues

Do not publish an exploitable security issue in a public issue before maintainers can assess it. Use the repository's private security reporting feature when it becomes available, or contact the maintainer privately. Include a minimal reproduction, affected version, impact, and suggested mitigation when possible.

Changes that weaken DTD/entity blocking, active-content rejection, Base64 isolation, CSP, or WebView2 restrictions require explicit security rationale and tests.

## Samples, assets, and copyright

Only contribute samples, text, images, layouts, coordinates, fonts, and other assets that you created yourself or have explicit permission to redistribute under compatible terms. State their origin and license in the pull request. Do not upload third-party archives or copy SVG files, diagrams, text, styling, or geometry from another project.
