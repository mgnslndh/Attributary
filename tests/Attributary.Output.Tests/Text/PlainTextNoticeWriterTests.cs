using Attributary.Artifacts;
using Attributary.Output.Text;

namespace Attributary.Output.Tests.Text;

public class PlainTextNoticeWriterTests
{
    [Test]
    public async Task Render_OneSection_IncludesComponentHeaderAndText()
    {
        var document = new NoticeDocument([new NoticeSection("Foo", "1.0.0", "Foo notice text")]);
        var writer = new PlainTextNoticeWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0");
        await Assert.That(result).Contains("Foo notice text");
    }
}
