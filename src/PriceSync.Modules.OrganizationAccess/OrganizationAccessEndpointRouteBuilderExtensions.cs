using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
            .WithTags("OrganizationAccess")
            .RequireAuthorization();

        api.MapGet("", ListMyOrganizationsEndpoint.HandleAsync)

            .WithName("ListMyOrganizations")
            .WithSummary("List the signed-in user's organizations")
            .WithDescription("Returns the organizations accessible to the signed-in user, so they can choose which organization to act on behalf of.");

        api.MapGet("/{organizationId:guid}", GetOrganizationEndpoint.HandleAsync)

            .WithName("GetOrganization")
            .WithSummary("Get organization details")
            .WithDescription("Returns details for the supplied organization ID when the signed-in user has access to that organization.");

        return endpoints;
    }
}
