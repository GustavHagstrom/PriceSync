using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Features.End;
using PriceSync.Modules.TradingRelationships.Infrastructure;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                "GetByIdEndpoint completed with {Outcome}, reason {Reason}",
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
        var result = await handler.HandleAsync(tradingRelationshipId, cancellationToken);
        var response = result switch
        {
            GetResult.Found found => Results.Ok(found.Response),
            _ => Results.NotFound()
        };

        return response;
    }
}
