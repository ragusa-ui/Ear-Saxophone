using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Input;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Actions;

/// <summary>Runs every kind of action. Media commands and keyboard shortcuts are separate actions and independent of any program.</summary>
public sealed class ActionExecutor(AudioController audio, IKeyboardSender keyboard)
{
    public ActionResult Execute(ActionInvocation invocation)
    {
        try
        {
            return invocation.Mapping.Action switch
            {
                SetVolumeAction or ToggleMuteAction => audio.Execute(invocation)
                    ?? new ActionResult(ActionOutcome.Failed, Texts.AudioActionFailed),
                MediaAction media => RunMedia(media),
                HotkeyAction hotkey => RunHotkey(hotkey),
                _ => new ActionResult(ActionOutcome.Failed, Texts.ActionUnsupported),
            };
        }
        catch (Exception ex)
        {
            return new ActionResult(ActionOutcome.Failed, Texts.ActionFailed(ex.Message));
        }
    }

    private ActionResult RunMedia(MediaAction action)
    {
        keyboard.SendMediaCommand(action.Command);
        return new ActionResult(ActionOutcome.Applied, Texts.MediaSent(action.Command));
    }

    private ActionResult RunHotkey(HotkeyAction action)
    {
        if (!Hotkey.TryParse(action.Keys, out Hotkey hotkey, out string error))
        {
            return new ActionResult(ActionOutcome.Failed, error);
        }

        keyboard.SendHotkey(hotkey);
        return new ActionResult(ActionOutcome.Applied, Texts.HotkeySent(hotkey.ToString()));
    }
}
