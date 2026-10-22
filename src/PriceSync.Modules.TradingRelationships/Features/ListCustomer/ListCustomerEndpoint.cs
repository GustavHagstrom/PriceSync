using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Infrastructure;
namespace PriceSync.Modules.TradingRelationships.Features.ListCustomer;

internal class ListCustomerEndpoint
{
    public static async Task<IResult> HandleAsync(
        [FromQuery] string? status,
        [FromQuery] string? cursor,
        ILogger<ListCustomerEndpoint> logger,
        ListCustomerHandler handler,
        ListCustomerValidator validator,
        CancellationToken cancellationToken)
    {
        using var scope = LogScope.BeginScope(logger, "ListCustomer", tradingRelationshipId: null, personId: null, organizationId: null);

        var validation = validator.Validate(status, cursor, out var query);
        if(validation.HasErrors)
        {
            logger.LogInformation("Validation failed");
            return Results.ValidationProblem(
                 validation.GetErrors(),
                 title: "Validation errors occurred",
                 statusCode: StatusCodes.Status400BadRequest,
                 extensions: new Dictionary<string, object?>
                 {
                    { "code", ListCustomerErrorCodes.ValidationFailed }
                 });
        }


        var result = await handler.HandleAsync(query, cancellationToken);
        var response = result switch
        {
            ListCustomerResult.Success success => Results.Ok(success.Response),
            ListCustomerResult.NotCapable notCapable => Results.Problem(
                title: "Forbidden",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?>
                {
                    { "code", ListCustomerErrorCodes.Forbidden }
                }),
            ListCustomerResult.ActingOrganizationMissing actingOrganizationMissing => Results.Problem(
                title: "Acting organization is missing",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    { "code", OrganizationContextErrors.Required }
                }),
            _ => throw new NotImplementedException()
        };

        return response;
    }
}

