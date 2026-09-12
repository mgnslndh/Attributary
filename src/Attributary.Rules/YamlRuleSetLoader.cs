using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Attributary.Rules;

public sealed class YamlRuleSetLoader : IRuleSetLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public RuleSet Load(string yamlContent)
    {
        var raw = _deserializer.Deserialize<RawRulesFile>(yamlContent);

        var unknownDefault = MapRule("*", raw.Defaults.UnknownLicense.Policy, raw.Defaults.UnknownLicense.Require, []);
        var rules = raw.Rules.Select(r => MapRule(r.Id, r.Policy, r.Require, r.Flags ?? [])).ToList();

        return new RuleSet(unknownDefault, rules);
    }

    private static LicenseRule MapRule(string idPattern, string policy, List<object> rawRequire, List<string> rawFlags)
    {
        var require = rawRequire.Select(MapObligation).ToList();
        var flags = rawFlags.Select(MapFlag).ToList();
        return new LicenseRule(idPattern, MapPolicy(policy), require, flags);
    }

    private static Obligation MapObligation(object raw)
    {
        if (raw is string simple)
            return new Obligation(MapObligationKind(simple), null);

        // notice-text: { when: upstream-notice-present } deserializes as a
        // single-entry Dictionary<object, object> from YamlDotNet.
        var map = (Dictionary<object, object>)raw;
        var kindName = (string)map.Keys.Single();
        var conditionMap = (Dictionary<object, object>)map.Values.Single();
        var condition = (string)conditionMap["when"];
        return new Obligation(MapObligationKind(kindName), condition);
    }

    private static ObligationKind MapObligationKind(string name) => name switch
    {
        "copyright" => ObligationKind.Copyright,
        "license-text" => ObligationKind.LicenseText,
        "notice-text" => ObligationKind.NoticeText,
        _ => throw new InvalidOperationException($"Unknown obligation '{name}'")
    };

    private static ObligationFlag MapFlag(string name) => name switch
    {
        "source-offer" => ObligationFlag.SourceOffer,
        "modification-disclosure" => ObligationFlag.ModificationDisclosure,
        "non-endorsement" => ObligationFlag.NonEndorsement,
        "trademark-non-grant" => ObligationFlag.TrademarkNonGrant,
        "patent-grant" => ObligationFlag.PatentGrant,
        "advertising-clause" => ObligationFlag.AdvertisingClause,
        "non-osi-approved" => ObligationFlag.NonOsiApproved,
        "copyleft-weak" => ObligationFlag.CopyleftWeak,
        "copyleft-strong" => ObligationFlag.CopyleftStrong,
        _ => throw new InvalidOperationException($"Unknown flag '{name}'")
    };

    private static LicensePolicy MapPolicy(string name) => name switch
    {
        "allow" => LicensePolicy.Allow,
        "warn" => LicensePolicy.Warn,
        "deny" => LicensePolicy.Deny,
        _ => throw new InvalidOperationException($"Unknown policy '{name}'")
    };

    private sealed class RawRulesFile
    {
        public RawDefaults Defaults { get; set; } = new();
        public List<RawRule> Rules { get; set; } = [];
    }

    private sealed class RawDefaults
    {
        public RawRule UnknownLicense { get; set; } = new();
    }

    private sealed class RawRule
    {
        public string Id { get; set; } = "";
        public string Policy { get; set; } = "deny";
        public List<object> Require { get; set; } = [];
        public List<string>? Flags { get; set; }
    }
}
