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

    /// <summary>Returns human-readable problems; an empty list means the profile is usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            problems.Add("Profile name is empty.");
        }

        var seen = new HashSet<ControlId>();
        foreach (ControlMapping mapping in Mappings)
        {
            if (!seen.Add(mapping.Control))
            {
                problems.Add($"{mapping.Control} is mapped more than once.");
            }

            if (!mapping.Action.IsValid)
            {
                problems.Add($"{mapping.Control}: the action is incomplete (missing target, device or keys).");
            }

            if (mapping.Scaling is { IsValid: false })
            {
                problems.Add($"{mapping.Control}: scaling range must satisfy 0 <= min <= max <= 1.");
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
