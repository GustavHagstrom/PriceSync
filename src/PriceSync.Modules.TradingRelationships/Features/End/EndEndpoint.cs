using Microsoft.AspNetCore.Http;
using PriceSync.Modules.TradingRelationships.Contracts;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.OrganizationAccess.Contracts;
using System.Diagnostics;

namespace PriceSync.Modules.TradingRelationships.Features.End;

internal class EndEndpoint
{
    internal static async Task<IResult> HandleAsync(
        Guid tradingRelationshipId,
        EndHandler handler,
        EndValidator validator,
        ILogger<EndEndpoint> logger,
        ICurrentPerson currentPerson,
        ICurrentOrganization currentOrganization,
        CancellationToken cancellationToken)
    {
        var command = new EndCommand(tradingRelationshipId == Guid.Empty
            ? default
            : TradingRelationshipId.FromGuid(tradingRelationshipId));
        var errors = validator.Validate(command);
        if (errors.HasErrors)
        {
            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["Operation"] = "EndTradingRelationship",
                ["PersonId"] = currentPerson.PersonId.Value,
                ["OrganizationId"] = currentOrganization.OrganizationId?.Value,
                ["TradingRelationshipId"] = tradingRelationshipId,
                ["TraceId"] = Activity.Current?.TraceId.ToString(),
            });
            logger.LogInformation(
                "EndTradingRelationship completed with {Outcome}, reason {Reason}",
                "Denied", "ValidationFailed");
            return Results.ValidationProblem(
                errors.GetErrors(),
                title: "Validation errors occurred",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    { "code", EndErrorCodes.ValidationFailed }
                });
        }
        var result = await handler.HandleAsync(command, cancellationToken);

        var response = result switch
        {
            EndResult.Ended ended => Results.Ok(ended.Response),

            EndResult.TradingRelationshipNotFound => Results.Problem(
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    { "code", EndErrorCodes.TradingRelationshipNotFound }
                }),

            EndResult.ActingOrganizationRequired => Results.Problem(
                title: "Acting organization is required",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    { "code", EndErrorCodes.ActingOrganizationRequired }
                }),

            EndResult.Forbidden => Results.Problem(
                title: "Forbidden",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?>
                {
                    { "code", EndErrorCodes.Forbidden }
                }),

            _ => throw new InvalidOperationException("Unexpected result type.")
        };

        return response;
    }
}
