namespace Attributary.Rules;

public sealed record LicenseRule(
    string IdPattern,
    LicensePolicy Policy,
    IReadOnlyList<Obligation> Require,
    IReadOnlyList<ObligationFlag> Flags);
