using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Localization;

/// <summary>What the small on-screen display shows after an action (title, optional level bar, optional detail line).</summary>
/// <param name="Title">Target, for example "Lautsprecher", "Discord.exe" or "Wiedergabegerät".</param>
/// <param name="Detail">Second line, for example the new device name or "Stumm".</param>
/// <param name="Level">Volume 0..1 for the bar; null hides the bar.</param>
/// <param name="Muted">Shows the muted look.</param>
public sealed record OsdMessage(string Title, string? Detail = null, double? Level = null, bool Muted = false);

public static class OsdMessageFactory
{
    /// <summary>Returns what to display for an executed mapping, or null when nothing should be shown (failures, shortcuts).</summary>
    public static OsdMessage? Create(ControlMapping mapping, ActionResult result)
    {
        if (result.IsFailure)
        {
            return null;
        }

        bool inactive = result.Outcome == ActionOutcome.Deferred;
        switch (mapping.Action)
        {
            case SetVolumeAction volume:
                return new OsdMessage(MappingDescriber.Describe(volume.Target), inactive ? Texts.OsdInactive : null, result.Level, result.Muted == true);
            case ToggleMuteAction mute:
                bool muted = result.Muted == true;
                return new OsdMessage(MappingDescriber.Describe(mute.Target), inactive ? Texts.OsdInactive : muted ? Texts.OsdMuted : Texts.OsdUnmuted, null, muted);
            case SwitchDeviceAction switchDevice:
                return new OsdMessage(Texts.DeviceKindName(switchDevice.Flow), result.Detail);
            case MediaAction media:
                return new OsdMessage(Texts.OsdTitleMedia, Texts.MediaCommandName(media.Command));
            default:
                return null;
        }
    }
}
