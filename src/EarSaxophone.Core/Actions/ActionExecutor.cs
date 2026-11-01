using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Input;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Actions;

/// <summary>Runs every kind of action. Media commands and keyboard shortcuts are separate actions and independent of any program.</summary>
public sealed class ActionExecutor(AudioController audio, IKeyboardSender keyboard)
{
    /// <summary>
    /// Runs one action. An action that does not react to the invocation's phase (for example a media key on release)
    /// does nothing and reports <see cref="Texts.ActionNotTriggered"/>. Exceptions never escape.
    /// </summary>
    public ActionResult Execute(ActionInvocation invocation)
    {
        try
        {
            if (!ActionRules.RespondsTo(invocation.Action, invocation.Phase))
            {
                return new ActionResult(ActionOutcome.Applied, Texts.ActionNotTriggered);
            }

            return invocation.Action switch
            {
                SetVolumeAction or MuteAction or SwitchDeviceAction => audio.Execute(invocation)
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

    /// <summary>
    /// Runs several executions (normally all executions of one control message) and returns one result per execution,
    /// in the same order. Toggle mute actions pressed together on the same mapping switch as one group
    /// (<see cref="AudioController.ToggleMute(IReadOnlyList{AudioTarget})"/>); everything else runs one by one.
    /// </summary>
    public IReadOnlyList<ActionResult> ExecuteAll(IReadOnlyList<ActionInvocation> invocations)
    {
        var results = new ActionResult?[invocations.Count];
        for (int i = 0; i < invocations.Count; i++)
        {
            if (results[i] is not null)
            {
                continue;
            }

            ActionInvocation invocation = invocations[i];
            if (!IsGroupToggle(invocation))
            {
                results[i] = Execute(invocation);
                continue;
            }

            List<int> group = Enumerable.Range(i, invocations.Count - i)
                .Where(j => IsGroupToggle(invocations[j]) && invocations[j].Mapping.Equals(invocation.Mapping))
                .ToList();
            IReadOnlyList<ActionResult> groupResults = ToggleGroup(group.Select(j => ((MuteAction)invocations[j].Action).Target).ToList());
            for (int k = 0; k < group.Count; k++)
            {
                results[group[k]] = groupResults[k];
            }
        }

        return results.Select(r => r!).ToList();
    }

    /// <summary>
    /// Runs all actions of a mapping as if the control was used: continuous actions with <paramref name="level"/>, the
    /// others for <paramref name="keyPhase"/> (pressed by default). One result per execution, in the order of the actions;
    /// actions that do not react to the phase are left out.
    /// </summary>
    public IReadOnlyList<ActionResult> ExecuteMapping(ControlMapping mapping, float level = 1f, ActionPhase keyPhase = ActionPhase.Pressed) =>
        ExecuteAll(ActionInvocation.ForMapping(mapping, keyPhase, level));

    private static bool IsGroupToggle(ActionInvocation invocation) =>
        invocation is { Phase: ActionPhase.Pressed, Action: MuteAction { Mode: MuteMode.Toggle } };

    private IReadOnlyList<ActionResult> ToggleGroup(IReadOnlyList<AudioTarget> targets)
    {
        try
        {
            return audio.ToggleMute(targets);
        }
        catch (Exception ex)
        {
            var failed = new ActionResult(ActionOutcome.Failed, Texts.ActionFailed(ex.Message));
            return targets.Select(_ => failed).ToList();
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
