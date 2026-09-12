using Attributary.Diagnostics;
using Attributary.Domain;

namespace Attributary.Resolution;

public interface ILicenseResolutionChain
{
    Task<LicenseResolution> ResolveAsync(SbomComponent component, IDiagnosticSink diagnostics, CancellationToken ct);
}
