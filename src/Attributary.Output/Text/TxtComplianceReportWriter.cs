using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Text;

public sealed class TxtComplianceReportWriter : IComplianceReportWriter
{
    public OutputFormat Format => OutputFormat.Txt;

    public string Render(ComplianceReportDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Compliance Report");
        sb.AppendLine(new string('=', 40));
        foreach (var entry in document.Entries)
            sb.AppendLine($"{entry.ComponentName} {entry.ComponentVersion} - {string.Join(" AND ", entry.LicenseIds)} (source: {(entry.LicenseTextSource?.ToString() ?? "unresolved")})");

        sb.AppendLine();
        sb.AppendLine("Flagged for review");
        sb.AppendLine(new string('-', 40));
        foreach (var flag in document.FlaggedForReview)
            sb.AppendLine($"{flag.ComponentName} {flag.ComponentVersion} - policy: {flag.Policy}, flags: {string.Join(", ", flag.Flags)}");

        return sb.ToString();
    }
}
