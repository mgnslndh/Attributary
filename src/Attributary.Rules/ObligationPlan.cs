namespace Attributary.Rules;

public sealed record ObligationPlan(
    Attributary.Domain.LicenseResolution Resolution,
    LicensePolicy Policy,
    IReadOnlyList<Obligation> Obligations,
    IReadOnlyList<ObligationFlag> Flags);
