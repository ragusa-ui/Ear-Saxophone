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
public sealed record ActionResult(ActionOutcome Outcome, string Message)
{
    public bool IsFailure => Outcome == ActionOutcome.Failed;
}
