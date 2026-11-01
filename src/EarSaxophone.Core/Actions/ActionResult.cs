namespace EarSaxophone.Core.Actions;

public enum ActionOutcome
{
    /// <summary>The action reached its target.</summary>
    Applied,

    /// <summary>The target is not active right now; the value is remembered and applied when it appears.</summary>
    Deferred,

    Failed,
}

/// <summary>Result of running an action; Message is meant to be shown to the user.</summary>
/// <param name="Outcome">Whether the action reached its target.</param>
/// <param name="Message">Text for the user.</param>
/// <param name="Muted">New mute state when the action changed it (for the on-screen display).</param>
/// <param name="Level">Volume level 0..1 that was set (for the on-screen display).</param>
/// <param name="Detail">Short extra text such as the name of the device that was switched to.</param>
public sealed record ActionResult(ActionOutcome Outcome, string Message, bool? Muted = null, float? Level = null, string? Detail = null)
{
    public bool IsFailure => Outcome == ActionOutcome.Failed;
}
