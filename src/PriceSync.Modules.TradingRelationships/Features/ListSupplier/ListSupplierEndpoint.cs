using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Infrastructure;
namespace PriceSync.Modules.TradingRelationships.Features.ListSupplier;

internal class ListSupplierEndpoint
{
    public static async Task<IResult> HandleAsync(
        [FromQuery] string? status,
        [FromQuery] string? cursor,
        ILogger<ListSupplierEndpoint> logger,
        ListSupplierHandler handler,
        ListSupplierValidator validator,
        CancellationToken cancellationToken)
    {
        using var scope = LogScope.BeginScope(logger, "ListSupplier", tradingRelationshipId: null, personId: null, organizationId: null);

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
                    { "code", ListSupplierErrorCodes.ValidationFailed }
                 });
        }


        var result = await handler.HandleAsync(query, cancellationToken);
        var response = result switch
        {
            ListSupplierResult.Success success => Results.Ok(success.Response),
            ListSupplierResult.NotCapable notCapable => Results.Problem(
                title: "Forbidden",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?>
                {
                    { "code", ListSupplierErrorCodes.Forbidden }
                }),
            ListSupplierResult.ActingOrganizationMissing actingOrganizationMissing => Results.Problem(
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

