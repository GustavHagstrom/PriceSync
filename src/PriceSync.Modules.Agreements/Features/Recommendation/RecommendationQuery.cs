namespace PriceSync.Modules.Agreements.Features.Recommendation;

internal sealed record RecommendationQuery(Guid AgreementId, DateOnly EvaluatedDate, DateTimeOffset KnowledgeCutoff);
