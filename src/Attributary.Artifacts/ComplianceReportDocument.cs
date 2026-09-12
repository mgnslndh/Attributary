using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts;

public sealed record ComplianceReportEntry(
    string ComponentName,
    string ComponentVersion,
    string LicenseId,
    ResolutionSourceStrategy? LicenseTextSource,
    IReadOnlyList<ObligationKind> SatisfiedObligations);

public sealed record ReviewFlagEntry(
    string ComponentName,
    string ComponentVersion,
    string LicenseId,
    IReadOnlyList<ObligationFlag> Flags,
    LicensePolicy Policy);

public sealed record ComplianceReportDocument(
    IReadOnlyList<ComplianceReportEntry> Entries,
    IReadOnlyList<ReviewFlagEntry> FlaggedForReview);
