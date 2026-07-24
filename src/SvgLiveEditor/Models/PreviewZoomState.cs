using System.Globalization;

namespace SvgLiveEditor.Models;

public enum PreviewZoomMode
{
    Fit,
    Manual
}

public readonly record struct PreviewZoomState(PreviewZoomMode Mode, double ManualScale)
{
    public static PreviewZoomState Fit { get; } = new(PreviewZoomMode.Fit, 1.0);

    public static PreviewZoomState At100Percent { get; } = new(PreviewZoomMode.Manual, 1.0);

    public string DisplayText => Mode == PreviewZoomMode.Fit
        ? "Fit"
        : $"{(ManualScale * 100).ToString("0", CultureInfo.InvariantCulture)}%";
}
