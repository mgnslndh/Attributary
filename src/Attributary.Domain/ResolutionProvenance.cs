namespace Attributary.Domain;

public enum ResolutionSourceStrategy { SbomEmbedded, LocalPackageCache, VcsRepository, SpdxCanonical, LicenseUrl, Evidence }

public sealed record FieldProvenance(ResolutionSourceStrategy Strategy, string? SourceUrl, DateTimeOffset ResolvedAtUtc, bool FromCache);

public sealed record ResolutionProvenance(
    FieldProvenance? LicenseTextProvenance,
    FieldProvenance? CopyrightProvenance,
    FieldProvenance? NoticeTextProvenance)
{
    public static readonly ResolutionProvenance Empty = new(null, null, null);
}
