using System.Windows;
using System.Windows.Input;
using SvgLiveEditor.Models;

namespace SvgLiveEditor;

public partial class TemplateGalleryWindow : Window
{
    public TemplateGalleryWindow(
        IReadOnlyList<SvgTemplateDefinition> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        InitializeComponent();
        DataContext = templates;
        Loaded += (_, _) => TemplateList.Focus();
    }

    public SvgTemplateDefinition? SelectedTemplate { get; private set; }

    private void OnOpenTemplateClick(
        object sender,
        RoutedEventArgs e)
    {
        OpenSelectedTemplate();
    }

    private void OnTemplateDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        OpenSelectedTemplate();
    }

    private void OpenSelectedTemplate()
    {
        if (TemplateList.SelectedItem is not SvgTemplateDefinition selected)
        {
            return;
        }

        SelectedTemplate = selected;
        DialogResult = true;
    }
}
