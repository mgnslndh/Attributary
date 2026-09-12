namespace Attributary.Rules;

public enum ObligationKind { Copyright, LicenseText, NoticeText }

public sealed record Obligation(ObligationKind Kind, string? Condition);
