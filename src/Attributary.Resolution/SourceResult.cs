using Attributary.Domain;

namespace Attributary.Resolution;

public sealed record SourceResult(
    bool Resolved,
    string? LicenseText,
    string? CopyrightText,
    string? NoticeText,
    FieldProvenance? Provenance)
{
    public static SourceResult Unresolved { get; } = new(false, null, null, null, null);
}
