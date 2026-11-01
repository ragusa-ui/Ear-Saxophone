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
    /// <summary>
    /// Returns what to display for the executions of one control message (results in the same order as the executions),
    /// or null when nothing should be shown. The display of the first execution that has one leads (its level and mute
    /// state). With several executions the title is the own name of the mapping, otherwise the leading title plus the
    /// number of further executions ("Discord.exe + 2"); a single execution of a named mapping shows the name too.
    /// "nicht aktiv" is only shown when no volume or mute target was reached (none of them plays right now).
    /// </summary>
    public static OsdMessage? Create(IReadOnlyList<ActionInvocation> invocations, IReadOnlyList<ActionResult> results)
    {
        int count = Math.Min(invocations.Count, results.Count);
        int lead = -1;
        OsdMessage? message = null;
        for (int i = 0; i < count && message is null; i++)
        {
            message = Create(invocations[i].Action, results[i]);
            lead = i;
        }

        if (message is null)
        {
            return null;
        }

        string? name = invocations[lead].Mapping.Name;
        string title = !string.IsNullOrWhiteSpace(name) ? name.Trim()
            : count > 1 ? Texts.OsdMoreTargets(message.Title, count - 1)
            : message.Title;
        if (count > 1 && message.Detail == Texts.OsdInactive)
        {
            // The leading program does not play right now, but another target was reached: show what that one reports.
            int reached = Enumerable.Range(0, count).FirstOrDefault(
                i => invocations[i].Action is SetVolumeAction or MuteAction && results[i].Outcome == ActionOutcome.Applied, -1);
            if (reached >= 0 && Create(invocations[reached].Action, results[reached]) is { } other)
            {
                message = other;
            }
        }

        return message with { Title = title };
    }

    /// <summary>Returns what to display for an executed action, or null when nothing should be shown (failures, shortcuts).</summary>
    public static OsdMessage? Create(ActionDefinition action, ActionResult result)
    {
        if (result.IsFailure)
        {
            return null;
        }

        bool inactive = result.Outcome == ActionOutcome.Deferred;
        switch (action)
        {
            case SetVolumeAction volume:
                return new OsdMessage(MappingDescriber.Describe(volume.Target), inactive ? Texts.OsdInactive : null, result.Level, result.Muted == true);
            case MuteAction mute:
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
