namespace Attributary.Domain;

public sealed record LicenseResolution(
    SbomComponent Component,
    string? ResolvedLicenseId,
    string? CopyrightText,
    string? LicenseText,
    string? NoticeText);
