using Attributary.Artifacts;

namespace Attributary.Output;

public interface IAttributionWriter
{
    OutputFormat Format { get; }
    string Render(AttributionDocument document);
}
