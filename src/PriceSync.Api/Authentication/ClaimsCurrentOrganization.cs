using PriceSync.Modules.OrganizationAccess.Contracts;
using System.Security.Claims;

namespace PriceSync.Api.Authentication;

internal sealed class ClaimsCurrentOrganization(IHttpContextAccessor httpContextAccessor) : ICurrentOrganization
{
    private const string OrganizationIdClaimType = "pricesync/organization_id";

    public OrganizationId OrganizationId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var claimValue = user?.FindFirstValue(OrganizationIdClaimType);
            if (!Guid.TryParse(claimValue, out var value))
            {
                throw new UnauthorizedAccessException(
                    "The authenticated principal does not contain a valid PriceSync OrganizationId.");
            }
            return new OrganizationId(value);
        }
    }
}
