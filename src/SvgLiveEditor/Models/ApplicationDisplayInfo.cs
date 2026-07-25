namespace SvgLiveEditor.Models;

public sealed record ApplicationDisplayInfo(
    string Name,
    string Version,
    string Architecture,
    string Description,
    string RepositoryUrl)
{
    public string CopyText =>
        $"{Name} {Version} ({Architecture}){Environment.NewLine}{RepositoryUrl}";
}
