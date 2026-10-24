using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Mapping;

/// <summary>Binds one physical control to one action. Scaling only matters for continuous actions.</summary>
public sealed record ControlMapping(ControlId Control, ActionDefinition Action, ValueScaling? Scaling = null)
{
    public ValueScaling EffectiveScaling => Scaling ?? new ValueScaling();
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

    /// <summary>Returns human-readable problems; an empty list means the profile is usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            problems.Add(Texts.ProfileNameEmpty);
        }

        var seen = new HashSet<ControlId>();
        foreach (ControlMapping mapping in Mappings)
        {
            if (!seen.Add(mapping.Control))
            {
                problems.Add(Texts.ControlMappedTwice(new ControlLabel(mapping.Control.ToString())));
            }

            if (!mapping.Action.IsValid)
            {
                problems.Add(Texts.ActionIncomplete(new ControlLabel(mapping.Control.ToString())));
            }

            if (mapping.Scaling is { IsValid: false })
            {
                problems.Add(Texts.ScalingInvalid(new ControlLabel(mapping.Control.ToString())));
            }
        }

        return problems;
    }
}

/// <summary>Everything persisted in the profile file.</summary>
public sealed record ProfileDocument(int SchemaVersion, string ActiveProfile, IReadOnlyList<Profile> Profiles)
{
    public const int CurrentSchemaVersion = 1;

    public static ProfileDocument CreateDefault() =>
        new(CurrentSchemaVersion, "Default", [new Profile("Default", [])]);
}
