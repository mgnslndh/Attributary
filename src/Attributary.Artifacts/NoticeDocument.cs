namespace Attributary.Artifacts;

public sealed record NoticeSection(string ComponentName, string ComponentVersion, string NoticeText);
public sealed record NoticeDocument(IReadOnlyList<NoticeSection> Sections);
