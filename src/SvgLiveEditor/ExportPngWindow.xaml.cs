using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor;

public partial class ExportPngWindow : Window
{
    private readonly SvgCanvasSize _canvasSize;
    private bool _updating = true;

    public ExportPngWindow(SvgCanvasSize canvasSize)
    {
        _canvasSize = canvasSize;
        InitializeComponent();
        Dimensions = new PreviewPngSizeCalculator().Calculate(canvasSize);
        WidthBox.Text = Dimensions.Width.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = Dimensions.Height.ToString(CultureInfo.InvariantCulture);
        _updating = false;
        ValidateDimensions();
        Loaded += (_, _) => { WidthBox.Focus(); WidthBox.SelectAll(); };
    }

    public PreviewPngSize Dimensions { get; private set; }

    private void OnWidthChanged(object sender, TextChangedEventArgs e) => UpdateDimensions(editingWidth: true);
    private void OnHeightChanged(object sender, TextChangedEventArgs e) => UpdateDimensions(editingWidth: false);
    private void OnLockRatioChecked(object sender, RoutedEventArgs e) => UpdateDimensions(editingWidth: true);

    private void UpdateDimensions(bool editingWidth)
    {
        if (_updating) return;
        _updating = true;
        if (LockRatio.IsChecked == true)
        {
            TextBox changed = editingWidth ? WidthBox : HeightBox;
            if (int.TryParse(changed.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int dimension)
                && dimension is >= 1 and <= PreviewPngSizeCalculator.MaximumDimension)
            {
                (editingWidth ? HeightBox : WidthBox).Text = PreviewPngSizeCalculator.LinkedDimension(
                    dimension, _canvasSize, editingWidth).ToString("0", CultureInfo.InvariantCulture);
            }
        }
        _updating = false;
        ValidateDimensions();
    }

    private void ValidateDimensions()
    {
        ExportButton.IsEnabled = PreviewPngSizeCalculator.TryReadExportSize(
            WidthBox.Text, HeightBox.Text, out _, out string error);
        ErrorText.Text = error;
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (!PreviewPngSizeCalculator.TryReadExportSize(WidthBox.Text, HeightBox.Text, out PreviewPngSize size, out _)) return;
        Dimensions = size;
        DialogResult = true;
    }
}
