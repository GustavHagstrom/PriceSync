using System.Security.Claims;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Api.Authentication;

internal sealed class ClaimsCurrentPerson(IHttpContextAccessor httpContextAccessor) : ICurrentPerson
{
    private const string PersonIdClaimType = "pricesync/person_id";

    public PersonId PersonId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var claimValue = user?.FindFirstValue(PersonIdClaimType);
            if (!Guid.TryParse(claimValue, out var value))
            {
                throw new UnauthorizedAccessException(
                    "The authenticated principal does not contain a valid PriceSync PersonId.");
            }

            return new PersonId(value);
        }
    }
}
