namespace Attributary.Artifacts;

public sealed record LicenseTextEntry(string LicenseId, string Text);
public sealed record LicenseTextsDocument(IReadOnlyList<LicenseTextEntry> Licenses);
