using Microsoft.AspNetCore.Http;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.OrganizationAccess.Features.GetOrganization;

internal static class GetOrganizationEndpoint
{
    public static async Task<IResult> HandleAsync(
        Guid organizationId,
        GetOrganizationHandler handler,
        CancellationToken cancellationToken)
    {
        GetOrganizationResponse? organization;
        try
        {
            organization = await handler.HandleAsync(new OrganizationId(organizationId), cancellationToken);
        }
        catch (ArgumentException)
        {
            return OrganizationNotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }

        return organization is null ? OrganizationNotFound() : Results.Ok(organization);
    }

    private static IResult OrganizationNotFound()
    {
        return Results.Problem(
            title: "Organization was not found.",
            statusCode: StatusCodes.Status404NotFound,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "organization_not_found",
            });
    }
}
