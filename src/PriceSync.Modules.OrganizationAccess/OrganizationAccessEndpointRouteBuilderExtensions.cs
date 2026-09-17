using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.OrganizationAccess.Features.GetOrganization;
using PriceSync.Modules.OrganizationAccess.Features.ListMyOrganizations;

namespace PriceSync.Modules.OrganizationAccess;

public static class OrganizationAccessEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapOrganizationAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints
            .MapGroup("/api/v1/organizations")
            .RequireAuthorization();

        api.MapGet("", ListMyOrganizationsEndpoint.HandleAsync)
            .WithName("ListMyOrganizations");

        api.MapGet("/{organizationId:guid}", GetOrganizationEndpoint.HandleAsync)
            .WithName("GetOrganization");

        return endpoints;
    }
}
