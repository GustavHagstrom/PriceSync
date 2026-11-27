using Microsoft.AspNetCore.Http;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.PricePublication.Domain;
using PriceSync.Modules.PricePublication.Persistence;

namespace PriceSync.Modules.PricePublication.Features.Create;

internal static class CreateEndpoint
{
    internal static async Task<IResult> HandleAsync(
        CreateRequest request, 
        ICurrentPerson person,
        ICurrentOrganization organization, 
        IOrganizationAuthorizer authorizer, 
        IOrganizationStatusReader statusReader, 
        PricePublicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } owner)
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextErrors.Required });
        if (!await authorizer.HasCapabilityAsync(person.PersonId, owner, OrganizationCapability.CreatePriceLists, cancellationToken))
            return Results.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { ["code"] = "forbidden" });
        PriceList list;
        try
        {
            list = PriceList.Create(owner, request.Name ?? "", request.Type ?? "", request.Currency ?? "", request.Market ?? "",
                request.CustomerOrganizationId is { } customer ? new OrganizationId(customer) : null);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [exception.ParamName ?? "request"] = [exception.Message] },
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });
        }
        if (list.CustomerOrganizationId is { } customerId &&
            await statusReader.GetStatusAsync(customerId, cancellationToken) != OrganizationStatusResult.Active)
            return Results.Problem(statusCode: 400,
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_customer_reference" });
        dbContext.PriceLists.Add(list);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/price-lists/{list.Id.Value}", PriceListResponse.From(list));
    }
}
