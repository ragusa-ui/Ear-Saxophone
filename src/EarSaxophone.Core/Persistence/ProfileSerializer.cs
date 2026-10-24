using System.Text.Json;
using System.Text.Json.Serialization;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Persistence;

public sealed class ProfileFormatException(string message, Exception? inner = null) : Exception(message, inner);

public static class ProfileSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(ProfileDocument document) => JsonSerializer.Serialize(document, Options);

    public static ProfileDocument Deserialize(string json)
    {
        ProfileDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ProfileDocument>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ProfileFormatException("The profile file is not valid JSON or has an unexpected structure.", ex);
        }

        if (document is null || document.Profiles is null)
        {
            throw new ProfileFormatException("The profile file is empty.");
        }

        if (document.SchemaVersion < 1 || document.SchemaVersion > ProfileDocument.CurrentSchemaVersion)
        {
            throw new ProfileFormatException(
                $"Unsupported profile file version {document.SchemaVersion} (this build supports up to {ProfileDocument.CurrentSchemaVersion}).");
        }

        return document;
    }
}
