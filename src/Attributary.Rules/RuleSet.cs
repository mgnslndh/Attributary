namespace Attributary.Rules;

public sealed record RuleSet(LicenseRule UnknownLicenseDefault, IReadOnlyList<LicenseRule> Rules);
