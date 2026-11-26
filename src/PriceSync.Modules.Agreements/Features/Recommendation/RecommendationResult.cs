using PriceSync.Modules.Agreements.Models;

namespace PriceSync.Modules.Agreements.Features.Recommendation;

internal abstract record RecommendationResult
{
    internal sealed record Success(VersionResponse? Recommendation) : RecommendationResult;
    internal sealed record ActingOrganizationRequired : RecommendationResult;
    internal sealed record Forbidden : RecommendationResult;
    internal sealed record NotFound : RecommendationResult;
}
