using Nirengi.BuildingBlocks.Application.RuleSets;
using Nirengi.Modules.Recommendation.Domain.Plans;

namespace Nirengi.Modules.Recommendation.Application;

internal sealed class RecommendationRuleSetDescriptor : IRuleSetDescriptor
{
    public RuleSetDescription Describe() =>
        RuleSetDescription.From("Recommendation", "Öneri kuralları", RecommendationRuleSet.V1.Version, RecommendationRuleSet.V1);
}
