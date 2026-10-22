using Microsoft.Extensions.Logging;
using PriceSync.Modules.TradingRelationships.Infrastructure;
using Microsoft.AspNetCore.Http;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.TradingRelationships.Features.Create;

internal class CreateEndpoint
{
    internal static async Task<IResult> HandleAsync(
        CreateRequest request,
        CreateHandler handler,
        CreateValidator validator,
        ILogger<CreateEndpoint> logger,
        ICurrentPerson currentPerson,
        ICurrentOrganization currentOrganization,
        CancellationToken cancellationToken)
    {
        using var scope = LogScope.BeginScope(logger, "CreateTradingRelationship",
            personId: currentPerson.PersonId.Value, organizationId: currentOrganization.OrganizationId?.Value);
        using var pairScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["SupplierOrganizationId"] = request.SupplierOrganizationId,
            ["CustomerOrganizationId"] = request.CustomerOrganizationId,
        });
        var command = new CreateCommand(new(request.SupplierOrganizationId), new(request.CustomerOrganizationId));

        var errors = validator.Validate(command);
        if (errors.HasErrors)
        {
            logger.LogInformation(
                "CreateTradingRelationship completed with {Outcome}, reason {Reason}",
                "Denied", "ValidationFailed");
            return Results.ValidationProblem(
                errors.GetErrors(),
                title: "Validation errors occurred",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    { "code", CreateErrorCodes.ValidationFailed }
                });
        }

        var result = await handler.HandleAsync(command, cancellationToken);
        var response = result switch
        {
            CreateResult.Created created => Results.Created($"{Routes.BaseV1}/{created.Response.Id}", created.Response),

            CreateResult.Existed existed => Results.Ok(existed.Response),

            CreateResult.ActingOrganizationRequired => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Current organization is missing",
                extensions: new Dictionary<string, object?>
                {
                    { "code", OrganizationContextErrors.Required }
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
