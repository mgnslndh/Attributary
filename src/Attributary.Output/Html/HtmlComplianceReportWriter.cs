using System.Net;
using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Html;

public sealed class HtmlComplianceReportWriter : IComplianceReportWriter
{
    public OutputFormat Format => OutputFormat.Html;

    public string Render(ComplianceReportDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<h1>Compliance Report</h1>");
        sb.AppendLine("<table><tr><th>Component</th><th>Version</th><th>License</th><th>Source</th></tr>");
        foreach (var entry in document.Entries)
            sb.AppendLine($"<tr><td>{Encode(entry.ComponentName)}</td><td>{Encode(entry.ComponentVersion)}</td>"
                + $"<td>{Encode(entry.LicenseId)}</td><td>{Encode(entry.LicenseTextSource?.ToString() ?? "unresolved")}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Flagged for review</h2>");
        sb.AppendLine("<table><tr><th>Component</th><th>Version</th><th>License</th><th>Policy</th><th>Flags</th></tr>");
        foreach (var flag in document.FlaggedForReview)
            sb.AppendLine($"<tr><td>{Encode(flag.ComponentName)}</td><td>{Encode(flag.ComponentVersion)}</td>"
                + $"<td>{Encode(flag.LicenseId)}</td><td>{Encode(flag.Policy.ToString())}</td><td>{Encode(string.Join(", ", flag.Flags))}</td></tr>");
        sb.AppendLine("</table>");

        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
