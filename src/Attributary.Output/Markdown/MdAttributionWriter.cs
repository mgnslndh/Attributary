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
            foreach (var group in document.Rows.GroupBy(r => r.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"## {group.Key}");
                sb.AppendLine();
                foreach (var row in group)
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
                sb.AppendLine($"| {row.ComponentName} | {row.ComponentVersion} | [{row.LicenseId}](LICENSES/{row.LicenseId}.txt) | {row.Copyright} |");
        }

        return sb.ToString();
    }
}
