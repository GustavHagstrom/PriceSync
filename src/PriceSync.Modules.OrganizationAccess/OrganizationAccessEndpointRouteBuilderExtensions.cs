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
            .WithTags("OraganizationAccess")
            .RequireAuthorization();

        api.MapGet("", ListMyOrganizationsEndpoint.HandleAsync)
            .WithName("ListMyOrganizations")
            .WithSummary("Lists all organizations the current user has access to.");

        api.MapGet("/{organizationId:guid}", GetOrganizationEndpoint.HandleAsync)
            .WithName("GetOrganization")
            .WithSummary("Gets the details of a specific organization.");

        return endpoints;
    }
}
