using Attributary.Domain;

namespace Attributary.Resolution;

public interface ILicenseSource
{
    ResolutionSourceStrategy Strategy { get; }
    Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct);
}
