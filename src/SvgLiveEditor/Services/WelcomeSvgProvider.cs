using System.IO;
using System.Reflection;
using System.Text;

namespace SvgLiveEditor.Services;

public sealed class WelcomeSvgProvider
{
    private const string ResourceName = "SvgLiveEditor.Samples.welcome.svg";

    public string Load()
    {
        Assembly assembly = typeof(WelcomeSvgProvider).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded sample '{ResourceName}' was not found.");
        using StreamReader reader = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
