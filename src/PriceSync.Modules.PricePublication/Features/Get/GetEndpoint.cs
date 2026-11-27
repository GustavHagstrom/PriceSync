using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.PricePublication.Persistence;

namespace PriceSync.Modules.PricePublication.Features.Get;

internal static class GetEndpoint
{
    internal static async Task<IResult> HandleAsync(
        Guid priceListId, 
        ICurrentPerson person,
        ICurrentOrganization organization, 
        IOrganizationAuthorizer authorizer, 
        PricePublicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } owner)
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextErrors.Required });
        if (!await authorizer.HasCapabilityAsync(person.PersonId, owner, OrganizationCapability.ReadPriceLists, cancellationToken))
            return Results.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { ["code"] = "forbidden" });
        if (priceListId == Guid.Empty) return Results.NotFound();
        var id = new PriceListId(priceListId);
        var list = await dbContext.PriceLists.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.OwnerOrganizationId == owner, cancellationToken);
        return list is null ? Results.NotFound() : Results.Ok(PriceListResponse.From(list));
    }
}
