using Microsoft.AspNetCore.Http;

namespace PriceSync.Modules.TradingRelationships.Features.Create;

internal class CreateEndpoint
{
    internal static async Task<IResult> HandleAsync(
        CreateCommand request,
        CreateHandler handler,
        CreateValidator validator,
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
                    { "code", CreateErrorCodes.ValidationFailed }
                });
        }

        var result = await handler.HandleAsync(request, cancellationToken);
        var response = result switch
        {
            CreateResult.Created created => Results.CreatedAtRoute(Routes.Names.Get, new { id = created.Response.Id }, created.Response),
            CreateResult.Existed existed => Results.Ok(existed.Response),
            CreateResult.ActingOrganizationRequired => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Current organization is missing",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateErrorCodes.ActingOrganizationRequired }
                }),
            CreateResult.Forbidden => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateErrorCodes.Forbidden }
                }),
            CreateResult.SupplierNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Supplier organization not found",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateErrorCodes.SupplierNotFound }
                }),
            CreateResult.CustomerNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Customer organization not found",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateErrorCodes.CustomerNotFound }
                }),
            CreateResult.SupplierOrganizationEnded => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Supplier organization has ended",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateErrorCodes.SupplierOrganizationEnded }
                }),
            CreateResult.CustomerOrganizationEnded => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Customer organization has ended",
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateErrorCodes.CustomerOrganizationEnded }
                }),
            _ => throw new InvalidOperationException("Unexpected result type.")
        };
        return response;
        
    }
}
