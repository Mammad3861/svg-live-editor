using System.IO;

namespace SvgLiveEditor.Services;

public sealed class RecoveryDirectoryProvider
{
    public string GetPath() => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "SvgLiveEditor",
        "Recovery");
}
