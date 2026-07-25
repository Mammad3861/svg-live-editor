using System.Reflection;
using System.Runtime.InteropServices;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class ApplicationInfoService
{
    public const string RepositoryUrl =
        "https://github.com/Mammad3861/svg-live-editor";

    public ApplicationDisplayInfo Create(
        Assembly applicationAssembly,
        Architecture? processArchitecture = null)
    {
        ArgumentNullException.ThrowIfNull(applicationAssembly);

        string? informationalVersion = applicationAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        string fallbackVersion =
            applicationAssembly.GetName().Version?.ToString(3) ?? "0.0.0";

        return new ApplicationDisplayInfo(
            "SvgLiveEditor",
            FormatVersion(informationalVersion ?? fallbackVersion),
            FormatArchitecture(
                processArchitecture ?? RuntimeInformation.ProcessArchitecture),
            "Edit UTF-8 SVG/XML source with a live, security-restricted preview.",
            RepositoryUrl);
    }

    public static string FormatVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        string trimmed = version.Trim();
        int metadataSeparator = trimmed.IndexOf('+');
        if (metadataSeparator >= 0)
        {
            trimmed = trimmed[..metadataSeparator];
        }

        return trimmed.StartsWith('v')
            ? trimmed
            : $"v{trimmed}";
    }

    public static string FormatArchitecture(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.X86 => "win-x86",
            Architecture.Arm64 => "win-arm64",
            Architecture.Arm => "win-arm",
            _ => $"win-{architecture.ToString().ToLowerInvariant()}"
        };
    }
}
