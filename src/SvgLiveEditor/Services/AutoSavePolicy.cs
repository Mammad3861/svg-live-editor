using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class AutoSavePolicy
{
    public AutoSaveValidationDecision Evaluate(
        SvgValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(validation);
        return validation.IsValid
            ? new AutoSaveValidationDecision(true, "Auto-saving...")
            : new AutoSaveValidationDecision(
                false,
                "Auto Save paused · Invalid SVG");
    }
}
