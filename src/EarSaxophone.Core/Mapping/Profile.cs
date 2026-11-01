using System.Text.Json.Serialization;
using EarSaxophone.Core.Devices;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Mapping;

/// <summary>
/// Binds one physical control to one or more actions (for example one fader for the volume of several programs).
/// Scaling only matters for continuous actions. Name is an optional own title such as "Spiele" for display.
/// </summary>
public sealed record ControlMapping
{
    // Parameter names follow the property names (like a positional record), so named arguments read the same.
    [JsonConstructor]
    public ControlMapping(ControlId Control, IReadOnlyList<ActionDefinition> Actions, ValueScaling? Scaling = null, string? Name = null)
    {
        this.Control = Control;
        this.Actions = Actions?.ToArray() ?? [];
        this.Scaling = Scaling;
        this.Name = Name;
    }

    /// <summary>A mapping with exactly one action (the usual case).</summary>
    public ControlMapping(ControlId Control, ActionDefinition Action, ValueScaling? Scaling = null, string? Name = null)
        : this(Control, [Action], Scaling, Name)
    {
    }

    public ControlId Control { get; init; }

    public IReadOnlyList<ActionDefinition> Actions { get; init; }

    public ValueScaling? Scaling { get; init; }

    /// <summary>Optional own title; null or empty means "describe by the actions".</summary>
    public string? Name { get; init; }

    [JsonIgnore]
    public ValueScaling EffectiveScaling => Scaling ?? new ValueScaling();

    /// <summary>The first action, or null for an empty mapping.</summary>
    [JsonIgnore]
    public ActionDefinition? FirstAction => Actions.Count > 0 ? Actions[0] : null;

    /// <summary>True when the mapping has at least one continuous action (volume).</summary>
    [JsonIgnore]
    public bool HasContinuousAction => Actions.Any(a => a.IsContinuous);

    public bool Equals(ControlMapping? other) =>
        other is not null
        && Control == other.Control
        && Actions.SequenceEqual(other.Actions)
        && Equals(Scaling, other.Scaling)
        && Name == other.Name;

    public override int GetHashCode() => HashCode.Combine(Control, Actions.Count, Scaling, Name);
}

public enum TransferMode
{
    /// <summary>The mapping leaves the source; a mapping on the target is moved to the source (swap).</summary>
    Move,

    /// <summary>The source keeps its mapping; the target gets a copy (replacing what it had).</summary>
    Copy,
}

public enum TransferOutcome
{
    Moved,
    Swapped,
    Copied,

    /// <summary>The actions do not fit the other control (for example volume onto a key); nothing was changed.</summary>
    Incompatible,

    /// <summary>Source and target are the same, or the source has no mapping; nothing was changed.</summary>
    NothingToDo,
}

/// <summary>Result of <see cref="Profile.TransferMapping"/>: the outcome and the profile afterwards (unchanged unless moved, swapped or copied).</summary>
public sealed record TransferResult(TransferOutcome Outcome, Profile Profile)
{
    public bool Changed => Outcome is TransferOutcome.Moved or TransferOutcome.Swapped or TransferOutcome.Copied;
}

public sealed record Profile(string Name, IReadOnlyList<ControlMapping> Mappings)
{
    public ControlMapping? Find(ControlId control)
    {
        foreach (ControlMapping mapping in Mappings)
        {
            if (mapping.Control == control)
            {
                return mapping;
            }
        }

        return null;
    }

    /// <summary>Returns a copy in which the control is bound to this mapping (replacing an existing binding).</summary>
    public Profile WithMapping(ControlMapping mapping) =>
        this with { Mappings = Mappings.Where(m => m.Control != mapping.Control).Append(mapping).ToList() };

    public Profile WithoutMapping(ControlId control) =>
        this with { Mappings = Mappings.Where(m => m.Control != control).ToList() };

    /// <summary>
    /// Moves or copies the mapping of <paramref name="from"/> to <paramref name="to"/> (drag and drop in the picture).
    /// Moving onto a mapped control swaps both mappings; copying replaces the target's mapping. When an action does not
    /// fit the shape of the control it would land on, nothing changes and the outcome is <see cref="TransferOutcome.Incompatible"/>.
    /// A "Wie Fader" target (<see cref="AudioTarget.Strip"/>) is relative: on a key or knob of another strip it means that strip's
    /// fader. Where it would mean nothing (a control outside the strips) or the fader itself, it is replaced by the targets it had
    /// at its old place; when there were none, those actions are left out, and a mapping left without actions is
    /// <see cref="TransferOutcome.Incompatible"/>.
    /// </summary>
    public TransferResult TransferMapping(ControlId from, ControlId to, TransferMode mode, ShapeLookup shapes, FaderLookup? faders = null)
    {
        ControlMapping? source = Find(from);
        if (from == to || source is null)
        {
            return new TransferResult(TransferOutcome.NothingToDo, this);
        }

        ControlMapping? target = Find(to);
        if (!ActionRules.IsAllowed(source, shapes(to)) || Relocate(source, to, faders) is not { } arriving)
        {
            return new TransferResult(TransferOutcome.Incompatible, this);
        }

        if (mode == TransferMode.Copy)
        {
            return new TransferResult(TransferOutcome.Copied, ReplaceOrAppend(to, arriving));
        }

        if (target is null)
        {
            return new TransferResult(TransferOutcome.Moved, ReplaceOrAppend(from, arriving));
        }

        if (!ActionRules.IsAllowed(target, shapes(from)) || Relocate(target, from, faders) is not { } returning)
        {
            return new TransferResult(TransferOutcome.Incompatible, this);
        }

        var swapped = Mappings.Select(m =>
            m.Control == from ? returning
            : m.Control == to ? arriving
            : m).ToList();
        return new TransferResult(TransferOutcome.Swapped, this with { Mappings = swapped });
    }

    /// <summary>
    /// The mapping as it lands on <paramref name="destination"/>: strip targets stay relative when the destination is a key or knob
    /// of a strip, otherwise they are replaced by the fader targets of the old place (resolved in this profile, before the change).
    /// Null when nothing would be left.
    /// </summary>
    private ControlMapping? Relocate(ControlMapping mapping, ControlId destination, FaderLookup? faders)
    {
        ControlMapping moved = mapping with { Control = destination };
        if (!StripTargets.HasStripTarget(mapping) || StripTargets.SourceFader(destination, faders) is not null)
        {
            return moved;
        }

        ControlMapping concrete = StripTargets.Materialize(moved, StripTargets.FaderTargets(this, mapping.Control, faders));
        return concrete.Actions.Count == 0 ? null : concrete;
    }

    /// <summary>
    /// Returns human-readable problems; an empty list means the profile is usable. Actions that do not fit the kind of
    /// control are reported too; <paramref name="shapes"/> defaults to the nanoKONTROL2 layout (unknown controls allow everything).
    /// A "Wie Fader" target outside a strip or on the fader itself is reported (it can never do anything there).
    /// </summary>
    public IReadOnlyList<string> Validate(ShapeLookup? shapes = null, FaderLookup? faders = null)
    {
        shapes ??= Nanokontrol2Layout.ShapeOf;
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            problems.Add(Texts.ProfileNameEmpty);
        }

        var seen = new HashSet<ControlId>();
        foreach (ControlMapping mapping in Mappings)
        {
            var label = new ControlLabel(mapping.Control.ToString());
            if (!seen.Add(mapping.Control))
            {
                problems.Add(Texts.ControlMappedTwice(label));
            }

            if (mapping.Actions.Count == 0)
            {
                problems.Add(Texts.MappingWithoutAction(label));
            }

            if (mapping.Actions.Any(a => a is null || !a.IsValid))
            {
                problems.Add(Texts.ActionIncomplete(label));
            }

            ControlShape shape = shapes(mapping.Control);
            foreach (ActionDefinition action in mapping.Actions.Where(a => a is not null && !ActionRules.IsAllowed(a, shape)).DistinctBy(a => a.GetType()))
            {
                problems.Add(Texts.ActionNotAllowed(label, MappingDescriber.KindName(action), shape));
            }

            if (StripTargets.HasStripTarget(mapping) && StripTargets.SourceFader(mapping.Control, faders) is null)
            {
                problems.Add(Texts.StripTargetMisplaced(label));
            }

            if (mapping.Scaling is { IsValid: false })
            {
                problems.Add(Texts.ScalingInvalid(label));
            }
        }

        return problems;
    }

    /// <summary>Puts the replacement where the mapping of <paramref name="control"/> was (or at the end) and drops other mappings of that control.</summary>
    private Profile ReplaceOrAppend(ControlId control, ControlMapping replacement)
    {
        var list = new List<ControlMapping>();
        bool placed = false;
        foreach (ControlMapping mapping in Mappings)
        {
            if (mapping.Control != control)
            {
                list.Add(mapping);
            }
            else if (!placed)
            {
                list.Add(replacement);
                placed = true;
            }
        }

        if (!placed)
        {
            list.Add(replacement);
        }

        return this with { Mappings = list };
    }
}

/// <summary>Everything persisted in the profile file.</summary>
public sealed record ProfileDocument(int SchemaVersion, string ActiveProfile, IReadOnlyList<Profile> Profiles)
{
    /// <summary>Version 2: each mapping has a list "actions" (version 1 had a single "action"; it is migrated on load).</summary>
    public const int CurrentSchemaVersion = 2;

    public static ProfileDocument CreateDefault() =>
        new(CurrentSchemaVersion, "Default", [new Profile("Default", [])]);
}
