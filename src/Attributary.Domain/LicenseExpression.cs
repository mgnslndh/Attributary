namespace Attributary.Domain;

public sealed record LicenseExpression(string? SpdxId, string? FreeTextName, string? SpdxExpression)
{
    public static LicenseExpression FromId(string id) => new(id, null, null);
    public static LicenseExpression FromName(string name) => new(null, name, null);
    public static LicenseExpression FromExpression(string expression) => new(null, null, expression);

    public bool IsSingleResolved => SpdxId is not null || FreeTextName is not null;
}
