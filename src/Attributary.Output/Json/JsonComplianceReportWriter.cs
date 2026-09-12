using System.Text.Json;
using System.Text.Json.Serialization;
using Attributary.Artifacts;

namespace Attributary.Output.Json;

public sealed class JsonComplianceReportWriter : IComplianceReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public OutputFormat Format => OutputFormat.Json;

    public string Render(ComplianceReportDocument document) => JsonSerializer.Serialize(document, Options);
}
