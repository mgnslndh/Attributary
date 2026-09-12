namespace Attributary.Artifacts;

public sealed record AttributionRow(string ComponentName, string ComponentVersion, string LicenseId, string Copyright);

public sealed record AttributionDocument(
    IReadOnlyList<AttributionRow> Rows,
    IReadOnlyDictionary<string, string> LicenseTextsById,
    bool GroupByLicense,
    bool EmbedLicenseText);
