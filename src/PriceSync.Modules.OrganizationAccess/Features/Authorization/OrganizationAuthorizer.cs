using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;

namespace PriceSync.Modules.OrganizationAccess.Features.Authorization;

internal class OrganizationAuthorizer(OrganizationAccessDbContext dbContext) : IOrganizationAuthorizer
{
    public async Task<bool> HasCapabilityAsync(PersonId personId, OrganizationId organizationId, OrganizationCapability capability, CancellationToken cancellationToken = default)
    {
        if(personId.Value == Guid.Empty)
        {
            return false;
        }
        if(organizationId.Value == Guid.Empty)
        {
            return false;
        }
        var existingOrganization = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId && o.Status == OrganizationStatus.Active, cancellationToken);
        if (existingOrganization == null)
        {
            return false;
        }
        var result = capability switch
        {
            OrganizationCapability.ReadTradingRelationships => await CheckReadTradingRelationshipCapability(personId, organizationId, cancellationToken),
            OrganizationCapability.ManageTradingRelationships => await CheckManageTradingRelationshipsCapability(personId, organizationId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(capability), capability, "Invalid capability value.")
        };
        return result;
    }
    private async Task<bool> CheckReadTradingRelationshipCapability(PersonId personId, OrganizationId organizationId, CancellationToken cancellationToken = default)
    {
        var membership = await dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.PersonId == personId && m.OrganizationId == organizationId && m.Status == OrganizationMembershipStatus.Active, cancellationToken);
        if (membership == null)
        {
            return false;
        }
        if (membership.Status != OrganizationMembershipStatus.Active)
        {
            return false;
        }
        return true;
    }
    private async Task<bool> CheckManageTradingRelationshipsCapability(PersonId personId, OrganizationId organizationId, CancellationToken cancellationToken = default)
    {
        var membership = await dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.PersonId == personId && m.OrganizationId == organizationId && m.Status == OrganizationMembershipStatus.Active, cancellationToken);
        if(membership == null)
        {
            return false;
        }
        if(membership.Status != OrganizationMembershipStatus.Active)
        {
            return false;
        }
        if(membership.Roles.Any(r => r == OrganizationRole.Owner))
        {
            return true;
        }
        return false;
    }
}
