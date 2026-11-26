using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Features.Recommendation;

internal static class RecommendationEndpoint
{
    internal static async Task<IResult> HandleAsync(Guid agreementId, [FromQuery] string? evaluatedDate,
        [FromQuery] string? knowledgeCutoff, RecommendationValidator validator, RecommendationHandler handler,
        CancellationToken cancellationToken)
    {
        var errors = validator.Validate(agreementId, evaluatedDate, knowledgeCutoff, out var query);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors, extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });

        return await handler.HandleAsync(query, cancellationToken) switch
        {
            RecommendationResult.Success success => Results.Ok(new
            {
                evaluatedDate = query.EvaluatedDate,
                knowledgeCutoff = query.KnowledgeCutoff,
                recommendation = success.Recommendation,
            }),
            RecommendationResult.ActingOrganizationRequired => Problem(400, OrganizationContextErrors.Required),
            RecommendationResult.Forbidden => Problem(403, "forbidden"),
            RecommendationResult.NotFound => Problem(404, "agreement_not_found"),
            _ => throw new InvalidOperationException("Unexpected result type."),
        };
    }

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
