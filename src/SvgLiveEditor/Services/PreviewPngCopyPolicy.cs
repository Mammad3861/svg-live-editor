using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewPngCopyPolicy
{
    private readonly PreviewPngSizeCalculator _sizeCalculator;

    public PreviewPngCopyPolicy(
        PreviewPngSizeCalculator? sizeCalculator = null)
    {
        _sizeCalculator =
            sizeCalculator ?? new PreviewPngSizeCalculator();
    }

    public bool TryCreatePlan(
        bool hasVisiblePreview,
        PreviewPngSourceState sourceState,
        SvgCanvasSize? lastValidCanvasSize,
        out PreviewPngCopyPlan? plan)
    {
        plan = null;
        if (!hasVisiblePreview
            || lastValidCanvasSize is not SvgCanvasSize canvasSize)
        {
            return false;
        }

        try
        {
            plan = new PreviewPngCopyPlan(
                _sizeCalculator.Calculate(canvasSize),
                sourceState);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
