using System.IO;

namespace SvgLiveEditor.Services;

public sealed class WebView2UserDataFolderProvider
{
    private readonly string _localApplicationData;

    public WebView2UserDataFolderProvider()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    public WebView2UserDataFolderProvider(string localApplicationData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);
        _localApplicationData = localApplicationData;
    }

    public string GetPath()
    {
        return Path.Combine(_localApplicationData, "SvgLiveEditor", "WebView2");
    }
}
