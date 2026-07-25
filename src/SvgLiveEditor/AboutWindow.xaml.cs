using System.Windows;
using SvgLiveEditor.Models;

namespace SvgLiveEditor;

public partial class AboutWindow : Window
{
    private readonly ApplicationDisplayInfo _displayInfo;

    public AboutWindow(ApplicationDisplayInfo displayInfo)
    {
        _displayInfo = displayInfo
            ?? throw new ArgumentNullException(nameof(displayInfo));
        InitializeComponent();
        DataContext = _displayInfo;
    }

    private void OnCopyVersionInformationClick(
        object sender,
        RoutedEventArgs e)
    {
        Clipboard.SetText(_displayInfo.CopyText);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
