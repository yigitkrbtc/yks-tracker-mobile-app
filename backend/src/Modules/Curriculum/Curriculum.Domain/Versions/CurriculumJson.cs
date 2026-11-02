using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nirengi.Modules.Curriculum.Domain.Versions;

/// <summary>How drafts, snapshots and calendars are stored (jsonb). Changing it needs a data migration.</summary>
public static class CurriculumJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },

        // jsonb reorders keys, so the "type" discriminator of an operation is not always first.
        AllowOutOfOrderMetadataProperties = true,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new JsonException($"Empty {typeof(T).Name} JSON.");

    public static IReadOnlyList<CurriculumOperation> Operations(string json) => Deserialize<List<CurriculumOperation>>(json);

    /// <summary>ETag and cross-module version of a published state: the seed (structure, weights) plus the snapshot.</summary>
    public static string ContentHash(string seedHash, string snapshot) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(seedHash + "\u001f" + snapshot)));
}
