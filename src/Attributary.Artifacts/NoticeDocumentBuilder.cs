using Attributary.Rules;

namespace Attributary.Artifacts;

public static class NoticeDocumentBuilder
{
    public static NoticeDocument Build(IReadOnlyList<ObligationPlan> plans)
    {
        var sections = plans
            .Where(p => p.Obligations.Any(o => o.Kind == ObligationKind.NoticeText) && p.Resolution.NoticeText is not null)
            .Select(p => new NoticeSection(p.Resolution.Component.Name, p.Resolution.Component.Version, p.Resolution.NoticeText!))
            .OrderBy(s => s.ComponentName, StringComparer.Ordinal)
            .ToList();

        return new NoticeDocument(sections);
    }
}
