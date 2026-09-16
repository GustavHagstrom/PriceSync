using Microsoft.AspNetCore.Http;

namespace PriceSync.Modules.OrganizationAccess.Features.ListMyOrganizations;

internal static class ListMyOrganizationsEndpoint
{
    public static async Task<IResult> HandleAsync(
        ListMyOrganizationsHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var organizations = await handler.HandleAsync(cancellationToken);
            return Results.Ok(organizations);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
    }
}
