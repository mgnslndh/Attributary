using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Markdown;

public sealed class MdAttributionWriter : IAttributionWriter
{
    public OutputFormat Format => OutputFormat.Md;

    public string Render(AttributionDocument document)
    {
        var sb = new StringBuilder();

        if (document.GroupByLicense)
        {
            var expanded = document.Rows.SelectMany(r => r.LicenseIds.Select(id => (LicenseId: id, Row: r)));
            foreach (var group in expanded.GroupBy(x => x.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"## {group.Key}");
                sb.AppendLine();
                foreach (var (_, row) in group)
                    sb.AppendLine($"- {row.ComponentName} {row.ComponentVersion} — {row.Copyright}");
                sb.AppendLine();

                if (document.EmbedLicenseText && document.LicenseTextsById.TryGetValue(group.Key, out var text))
                {
                    sb.AppendLine("```");
                    sb.AppendLine(text);
                    sb.AppendLine("```");
                }
                else
                {
                    sb.AppendLine($"[View full license text](LICENSES/{group.Key}.txt)");
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("| Component | Version | License | Copyright |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var row in document.Rows)
            {
                var licenseCell = string.Join(" AND ", row.LicenseIds.Select(id => $"[{id}](LICENSES/{id}.txt)"));
                sb.AppendLine($"| {row.ComponentName} | {row.ComponentVersion} | {licenseCell} | {row.Copyright} |");
            }
        }

        return sb.ToString();
    }
}
