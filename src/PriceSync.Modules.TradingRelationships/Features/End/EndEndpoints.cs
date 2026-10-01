using Microsoft.AspNetCore.Http;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.TradingRelationships.Features.End;

internal class EndEndpoints
{
    internal static async Task<IResult> HandleAsync(
        Guid tradingRelationshipId,
        EndHandler handler,
        EndValidator validator,
        CancellationToken cancellationToken)
    {
        var command = new EndCommand(tradingRelationshipId == Guid.Empty
            ? default
            : TradingRelationshipId.FromGuid(tradingRelationshipId));
        var errors = validator.Validate(command);
        if (errors.HasErrors)
        {
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
                title: "Trading relationship not found",
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

            EndResult.ActingOrganizationEnded => Results.Problem(
                title: "Acting organization has ended",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    { "code", EndErrorCodes.ActingOrganizationEnded }
                }),

            EndResult.ActingOrganizationNotFound => Results.Problem(
                title: "Acting organization not found",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    { "code", EndErrorCodes.ActingOrganizationNotFound }
                }),

            EndResult.ActingOrganizationNotMemberOfTradingRelationship => Results.Problem(
                title: "Acting organization is not a member of the trading relationship",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    { "code", EndErrorCodes.ActingOrganizationNotMemberOfTradingRelationship }
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
