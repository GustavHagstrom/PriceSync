using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;


namespace PriceSync.Modules.OrganizationAccess.Features.Status;

internal class OrganizationStatusReader(OrganizationAccessDbContext dbContext) : IOrganizationStatusReader
{
    public async Task<OrganizationStatusResult> GetStatusAsync(OrganizationId organizationId, CancellationToken cancellationToken = default)
    {
        if(organizationId.Value == Guid.Empty)
        {
            return OrganizationStatusResult.NotFound;
        }
        var organization = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);
        if (organization is null)
        {
            return OrganizationStatusResult.NotFound;
        }
        if(organization.Status == OrganizationStatus.Active)
        {
            return OrganizationStatusResult.Active;
        }
        if(organization.Status == OrganizationStatus.Archived)
        {
            return OrganizationStatusResult.Archived;
        }
        throw new InvalidOperationException($"Unexpected organization status: {organization.Status}");
    }
}
