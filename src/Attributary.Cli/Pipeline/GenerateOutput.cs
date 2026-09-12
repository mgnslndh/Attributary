using Attributary.Artifacts;
using Attributary.Rules;

namespace Attributary.Cli.Pipeline;

public sealed record GenerateOutput(
    IReadOnlyList<ObligationPlan> Plans,
    LicenseTextsDocument LicenseTexts,
    NoticeDocument Notice,
    AttributionDocument Attribution,
    ComplianceReportDocument Report);
