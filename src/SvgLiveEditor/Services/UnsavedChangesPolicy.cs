using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class UnsavedChangesPolicy
{
    public bool CanProceed(
        bool hasUnsavedChanges,
        UnsavedChangesChoice choice,
        bool saveSucceeded)
    {
        if (!hasUnsavedChanges)
        {
            return true;
        }

        return choice switch
        {
            UnsavedChangesChoice.Discard => true,
            UnsavedChangesChoice.Save => saveSucceeded,
            _ => false
        };
    }
}
