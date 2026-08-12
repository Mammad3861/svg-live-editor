using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewPointerGestureArbiter
{
    public PreviewPointerGestureAction Resolve(
        PreviewPointerGestureInput input)
    {
        if (input.Button == 1)
        {
            return input.ControlHeld
                || input.ShiftHeld
                || input.AltHeld
                || input.MetaHeld
                || input.SpaceHeld
                    ? PreviewPointerGestureAction.None
                    : PreviewPointerGestureAction.Pan;
        }

        if (input.Button != 0)
        {
            return PreviewPointerGestureAction.None;
        }

        if (input.PanModeEnabled || input.SpaceHeld)
        {
            return input.ControlHeld
                || input.ShiftHeld
                || input.AltHeld
                || input.MetaHeld
                    ? PreviewPointerGestureAction.None
                    : PreviewPointerGestureAction.Pan;
        }

        bool hasOutboundDragModifier =
            (input.ControlHeld && !input.AltHeld)
            || (input.AltHeld && !input.ControlHeld);
        if (!input.StartedOnArtwork
            || !input.IsPrimary
            || !input.IsMouse
            || input.ShiftHeld
            || !hasOutboundDragModifier
            || input.MetaHeld)
        {
            return PreviewPointerGestureAction.None;
        }

        return PreviewPointerGestureAction.OutboundDrag;
    }
}
