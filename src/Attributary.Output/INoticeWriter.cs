using Attributary.Artifacts;

namespace Attributary.Output;

public interface INoticeWriter
{
    string Render(NoticeDocument document);
}
