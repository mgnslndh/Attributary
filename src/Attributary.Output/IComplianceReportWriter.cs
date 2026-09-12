using Attributary.Artifacts;

namespace Attributary.Output;

public interface IComplianceReportWriter
{
    OutputFormat Format { get; }
    string Render(ComplianceReportDocument document);
}
