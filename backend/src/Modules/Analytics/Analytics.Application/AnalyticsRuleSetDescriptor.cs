using Nirengi.BuildingBlocks.Application.RuleSets;
using Nirengi.Modules.Analytics.Engine;

namespace Nirengi.Modules.Analytics.Application;

internal sealed class AnalyticsRuleSetDescriptor : IRuleSetDescriptor
{
    public RuleSetDescription Describe() =>
        RuleSetDescription.From("Analytics", "Analiz motoru (M1–M11)", RuleSet.V1.Version, RuleSet.V1);
}
