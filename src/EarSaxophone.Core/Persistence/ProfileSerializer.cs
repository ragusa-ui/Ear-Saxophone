using EarSaxophone.Core.Localization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Persistence;

public sealed class ProfileFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>A loaded document and the schema version the text had before migration.</summary>
public sealed record ProfileReadResult(ProfileDocument Document, int SourceSchemaVersion)
{
    public bool WasMigrated => SourceSchemaVersion != ProfileDocument.CurrentSchemaVersion;
}

/// <summary>
/// Reads and writes the profile file. Version 1 files (one "action" per mapping, mute as "toggleMute") are migrated
/// to version 2 ("actions" list, "mute" with mode) without loss while reading. Newer versions are refused.
/// </summary>
public static class ProfileSerializer
{
    private const string LegacyToggleMute = "toggleMute";
    private const string MuteTypeName = "mute";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IgnoreReadOnlyProperties = true,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(ProfileDocument document) =>
        JsonSerializer.Serialize(document with { SchemaVersion = ProfileDocument.CurrentSchemaVersion }, Options);

    public static ProfileDocument Deserialize(string json) => Read(json).Document;

    /// <summary>Parses, checks the version, migrates older versions and returns the document in the current version.</summary>
    public static ProfileReadResult Read(string json)
    {
        JsonObject root;
        int version;
        try
        {
            JsonNode? node = JsonNode.Parse(json);
            if (node is null)
            {
                throw new ProfileFormatException(Texts.ProfileFileEmpty);
            }

            root = node as JsonObject ?? throw new ProfileFormatException(Texts.ProfileFileInvalid);
            version = root["schemaVersion"]?.GetValue<int>() ?? 0;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new ProfileFormatException(Texts.ProfileFileInvalid, ex);
        }

        if (version < 1 || version > ProfileDocument.CurrentSchemaVersion)
        {
            throw new ProfileFormatException(Texts.ProfileFileVersion(version, ProfileDocument.CurrentSchemaVersion));
        }

        ProfileDocument? document;
        try
        {
            MigrateInPlace(root, version);
            document = root.Deserialize<ProfileDocument>(Options);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException or FormatException)
        {
            throw new ProfileFormatException(Texts.ProfileFileInvalid, ex);
        }

        if (document is null || document.Profiles is null)
        {
            throw new ProfileFormatException(Texts.ProfileFileEmpty);
        }

        if (document.Profiles.Any(p => p is null || p.Mappings is null || p.Mappings.Any(m => m is null || m.Actions.Any(a => a is null))))
        {
            throw new ProfileFormatException(Texts.ProfileFileInvalid);
        }

        return new ProfileReadResult(document with { SchemaVersion = ProfileDocument.CurrentSchemaVersion }, version);
    }

    /// <summary>Reads only the schema version of a profile text; null when it is not readable.</summary>
    public static int? PeekSchemaVersion(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject root ? root["schemaVersion"]?.GetValue<int>() : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Version 1 to 2: "action": {...} becomes "actions": [{...}] (a missing or null action becomes an empty list).
    /// In every version the old type name "toggleMute" is read as "mute" (mode toggle, the default).
    /// </summary>
    private static void MigrateInPlace(JsonObject root, int version)
    {
        if (root["profiles"] is not JsonArray profiles)
        {
            return;
        }

        foreach (JsonObject mapping in profiles.OfType<JsonObject>()
                     .Select(p => p["mappings"]).OfType<JsonArray>()
                     .SelectMany(m => m.OfType<JsonObject>()))
        {
            if (version == 1 && !mapping.ContainsKey("actions"))
            {
                JsonNode? single = mapping["action"];
                mapping.Remove("action");
                mapping["actions"] = single is null ? new JsonArray() : new JsonArray(single);
            }

            if (mapping["actions"] is JsonArray actions)
            {
                foreach (JsonObject action in actions.OfType<JsonObject>())
                {
                    if (action["type"] is JsonValue type && type.TryGetValue(out string? name) && name == LegacyToggleMute)
                    {
                        action["type"] = MuteTypeName;
                    }
                }
            }
        }
    }
}
