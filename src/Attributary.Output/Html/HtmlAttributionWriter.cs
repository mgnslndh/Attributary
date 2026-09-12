using System.Net;
using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Html;

public sealed class HtmlAttributionWriter : IAttributionWriter
{
    public OutputFormat Format => OutputFormat.Html;

    public string Render(AttributionDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<h1>Third-Party Notices</h1>");

        if (document.GroupByLicense)
        {
            foreach (var group in document.Rows.GroupBy(r => r.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"<h2>{Encode(group.Key)}</h2>");
                sb.AppendLine("<ul>");
                foreach (var row in group)
                    sb.AppendLine($"<li>{Encode(row.ComponentName)} {Encode(row.ComponentVersion)} — {Encode(row.Copyright)}</li>");
                sb.AppendLine("</ul>");

                if (document.EmbedLicenseText && document.LicenseTextsById.TryGetValue(group.Key, out var text))
                    sb.AppendLine($"<pre>{Encode(text)}</pre>");
                else
                    sb.AppendLine($"<p><a href=\"LICENSES/{Encode(group.Key)}.txt\">View full license text</a></p>");
            }
        }
        else
        {
            sb.AppendLine("<table><tr><th>Component</th><th>Version</th><th>License</th><th>Copyright</th></tr>");
            foreach (var row in document.Rows)
                sb.AppendLine($"<tr><td>{Encode(row.ComponentName)}</td><td>{Encode(row.ComponentVersion)}</td>"
                    + $"<td><a href=\"LICENSES/{Encode(row.LicenseId)}.txt\">{Encode(row.LicenseId)}</a></td><td>{Encode(row.Copyright)}</td></tr>");
            sb.AppendLine("</table>");
        }

        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
