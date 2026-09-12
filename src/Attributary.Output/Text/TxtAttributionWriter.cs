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
            foreach (var group in document.Rows.GroupBy(r => r.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"License: {group.Key}");
                sb.AppendLine(new string('-', 40));
                foreach (var row in group)
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
                sb.AppendLine($"{row.ComponentName} {row.ComponentVersion} | {row.LicenseId} | {row.Copyright}");

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
