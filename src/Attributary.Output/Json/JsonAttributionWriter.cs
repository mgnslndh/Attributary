using System.Text.Json;
using System.Text.Json.Serialization;
using Attributary.Artifacts;

namespace Attributary.Output.Json;

public sealed class JsonAttributionWriter : IAttributionWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public OutputFormat Format => OutputFormat.Json;

    public string Render(AttributionDocument document)
    {
        var payload = new
        {
            components = document.Rows.Select(r => new { r.ComponentName, r.ComponentVersion, r.LicenseId, r.Copyright }),
            licenses = document.LicenseTextsById.ToDictionary(
                kv => kv.Key,
                kv => document.EmbedLicenseText ? kv.Value : (string?)null)
        };
        return JsonSerializer.Serialize(payload, Options);
    }
}
