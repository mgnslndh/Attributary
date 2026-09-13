using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Text;

public sealed class TxtAttributionWriter : IAttributionWriter
{
    public OutputFormat Format => OutputFormat.Txt;

    public string Render(AttributionDocument document)
    {
        var sb = new StringBuilder();

        if (document.GroupByLicense)
        {
            var expanded = document.Rows.SelectMany(r => r.LicenseIds.Select(id => (LicenseId: id, Row: r)));
            foreach (var group in expanded.GroupBy(x => x.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"License: {group.Key}");
                sb.AppendLine(new string('-', 40));
                foreach (var (_, row) in group)
                    sb.AppendLine($"{row.ComponentName} {row.ComponentVersion} - {row.Copyright}");

                if (document.EmbedLicenseText && document.LicenseTextsById.TryGetValue(group.Key, out var text))
                {
                    sb.AppendLine();
                    sb.AppendLine(text);
                }
                sb.AppendLine();
            }
        }
        else
        {
            foreach (var row in document.Rows)
                sb.AppendLine($"{row.ComponentName} {row.ComponentVersion} | {string.Join(" AND ", row.LicenseIds)} | {row.Copyright}");

            if (document.EmbedLicenseText)
            {
                sb.AppendLine();
                sb.AppendLine("Licenses");
                sb.AppendLine(new string('=', 40));
                foreach (var (licenseId, text) in document.LicenseTextsById.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    sb.AppendLine($"License: {licenseId}");
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }
}
