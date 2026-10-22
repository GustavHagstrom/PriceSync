using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Infrastructure;

namespace PriceSync.Modules.TradingRelationships.Features.Get;

internal class GetEndpoint
{
    public static async Task<IResult> HandleAsync(
        Guid tradingRelationshipId,
        GetHandler handler,
        ICurrentPerson currentPerson,
        ICurrentOrganization currentOrganization,
        ILogger<GetEndpoint> logger,
        CancellationToken cancellationToken)
    {
        using var scope = LogScope.BeginScope(logger, "GetByIdEndpoint", tradingRelationshipId, currentPerson.PersonId.Value, currentOrganization.OrganizationId?.Value);
    
        if (tradingRelationshipId == Guid.Empty)
        {
            logger.LogInformation(
                "GetEndpoint completed with {Outcome}, reason {Reason}",
                "Denied", "ValidationFailed");
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed",
                detail: "Trading relationship ID cannot be empty.",
                extensions: new Dictionary<string, object?>
                {
                    { "code", GetErrorCodes.ValidationFailed }
                });
        }
        var result = await handler.HandleAsync(TradingRelationshipId.FromGuid(tradingRelationshipId), cancellationToken);
        var response = result switch
        {
            GetResult.Found found => Results.Ok(found.Response),
            GetResult.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: "Trading relationship not found.",
                extensions: new Dictionary<string, object?>
                {
                    { "code", GetErrorCodes.NotFound }
                }),
            GetResult.NotCapable => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You are missing capability to access this trading relationship.",
                extensions: new Dictionary<string, object?>
                {
                    { "code", GetErrorCodes.Forbidden }
                }),
            GetResult.OrganizationMissing => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "The organization is missing.",
                extensions: new Dictionary<string, object?>
                {
                    { "code", OrganizationContextErrors.Required}
                }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };

        return response;
    }
}
