using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Markdown;

public sealed class MdComplianceReportWriter : IComplianceReportWriter
{
    public OutputFormat Format => OutputFormat.Md;

    public string Render(ComplianceReportDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Compliance Report");
        sb.AppendLine();
        sb.AppendLine("| Component | Version | License | Source |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var entry in document.Entries)
            sb.AppendLine($"| {entry.ComponentName} | {entry.ComponentVersion} | {string.Join(" AND ", entry.LicenseIds)} | {(entry.LicenseTextSource?.ToString() ?? "unresolved")} |");

        sb.AppendLine();
        sb.AppendLine("## Flagged for review");
        sb.AppendLine();
        sb.AppendLine("| Component | Version | License | Policy | Flags |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var flag in document.FlaggedForReview)
            sb.AppendLine($"| {flag.ComponentName} | {flag.ComponentVersion} | {string.Join(" AND ", flag.LicenseIds)} | {flag.Policy} | {string.Join(", ", flag.Flags)} |");

        return sb.ToString();
    }
}
