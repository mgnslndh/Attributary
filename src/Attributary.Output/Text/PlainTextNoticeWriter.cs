using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Text;

public sealed class PlainTextNoticeWriter : INoticeWriter
{
    public string Render(NoticeDocument document)
    {
        var sb = new StringBuilder();
        foreach (var section in document.Sections)
        {
            sb.AppendLine(new string('-', 40));
            sb.AppendLine($"{section.ComponentName} {section.ComponentVersion}");
            sb.AppendLine(new string('-', 40));
            sb.AppendLine(section.NoticeText);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
