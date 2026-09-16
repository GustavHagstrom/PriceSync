using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.OrganizationAccess.Features.GetOrganization;

namespace PriceSync.Modules.OrganizationAccess;

public static class OrganizationAccessEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapOrganizationAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints
            .MapGroup("/api/v1/organizations")
            .RequireAuthorization();

        api.MapGet("/{organizationId:guid}", GetOrganizationEndpoint.HandleAsync)
            .WithName("GetOrganization");

        return endpoints;
    }
}
