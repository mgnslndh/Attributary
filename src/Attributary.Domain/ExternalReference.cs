namespace Attributary.Domain;

public enum ExternalReferenceType { Vcs, License, Website, Distribution, Other }

public sealed record ExternalReference(ExternalReferenceType Type, string Url);
