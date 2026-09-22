using PriceSync.Modules.OrganizationAccess.Contracts;
using System.Security.Claims;

namespace PriceSync.Api.Authentication;

internal sealed class CurrentOrganization(IHttpContextAccessor httpContextAccessor) : ICurrentOrganization
{
    private const string OrganizationIdHeader = "organization_id";

    public OrganizationId? OrganizationId
    {
        get
        {
            var organizationId = httpContextAccessor.HttpContext?.Request.Headers[OrganizationIdHeader].ToString();
            if(!Guid.TryParse(organizationId, out var value) || value == Guid.Empty)
            {
                return null;
            }
            return new OrganizationId(value);
        }
    }
}
