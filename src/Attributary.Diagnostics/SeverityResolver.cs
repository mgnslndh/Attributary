namespace Attributary.Diagnostics;

public static class SeverityResolver
{
    public static DiagnosticSeverity Resolve(DiagnosticDescriptor descriptor, SeverityOverrides overrides)
    {
        // Precedence: explicit > nowarn > warnaserror-exempt > warnaserror > default
        if (overrides.ExplicitSeverities.TryGetValue(descriptor.Code, out var explicitSeverity))
            return explicitSeverity;

        if (overrides.NoWarnCodes.Contains(descriptor.Code))
            return DiagnosticSeverity.Info;

        if (overrides.WarnAsErrorExemptCodes.Contains(descriptor.Code))
            return descriptor.DefaultSeverity;

        var promoteToError = overrides.WarnAsErrorAll || overrides.WarnAsErrorCodes.Contains(descriptor.Code);
        if (promoteToError && descriptor.DefaultSeverity == DiagnosticSeverity.Warning)
            return DiagnosticSeverity.Error;

        return descriptor.DefaultSeverity;
    }
}
