using Microsoft.AspNetCore.Http;

namespace PriceSync.Modules.TradingRelationships.Features.CreateTradingRelationship;

internal class CreateTradingRelationshipEndpoint
{
    internal static async Task<IResult> HandleAsync(
        CreateTradingRelationshipCommand request,
        CreateTradingRelationshipHandler handler,
        CreateTradingRelationshipValidator validator,
        CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.HasErrors)
        {
            return Results.ValidationProblem(
                errors.GetErrors(), 
                title: "Validation errors occurred",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateTradingRelationshipErrorCodes.ValidationFailed }
                });
        }

        var result = await handler.HandleAsync(request, cancellationToken);
        var response = result switch
        {
            CreateTradingRelationshipResult.Created created => Results.Created($"api/v1/trading-relationships/{created.Id}", new { created.Id.Value, SupplierId = created.SupplierId.Value, CustomerId =  created.CustomerId.Value, created.CreatedAt, created.Status }),
            CreateTradingRelationshipResult.Existed existed => Results.Ok(new { existed.Id.Value, SupplierId = existed.SupplierId.Value, CustomerId = existed.CustomerId.Value, existed.CreatedAt, existed.Status }),
            CreateTradingRelationshipResult.MissingCurrentOrganization => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Current organization is missing",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateTradingRelationshipErrorCodes.MissingCurrentOrganization }
                }),
            CreateTradingRelationshipResult.Forbidden => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateTradingRelationshipErrorCodes.Forbidden }
                }),
            CreateTradingRelationshipResult.SupplierNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Supplier organization not found",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateTradingRelationshipErrorCodes.SupplierNotFound }
                }),
            CreateTradingRelationshipResult.CustomerNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Customer organization not found",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateTradingRelationshipErrorCodes.CustomerNotFound }
                }),
            CreateTradingRelationshipResult.SupplierOrganizationEnded => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Supplier organization has ended",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateTradingRelationshipErrorCodes.SupplierOrganizationEnded }
                }),
            CreateTradingRelationshipResult.CustomerOrganizationEnded => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Customer organization has ended",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateTradingRelationshipErrorCodes.CustomerOrganizationEnded }
                }),
            _ => throw new InvalidOperationException("Unexpected result type.")
        };
        return response;
        
    }
}
