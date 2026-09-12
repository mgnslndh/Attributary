namespace Attributary.Domain;

public sealed record SbomComponent(
    string Name,
    string Version,
    string? Purl,
    LicenseExpression DeclaredLicense,
    string? RawCopyright,
    IReadOnlyList<ExternalReference> ExternalReferences,
    IReadOnlyList<LicenseEvidence> Evidence);
