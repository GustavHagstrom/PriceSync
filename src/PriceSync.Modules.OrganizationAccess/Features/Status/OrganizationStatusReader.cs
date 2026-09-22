using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;

namespace PriceSync.Modules.OrganizationAccess.Features.Status;

internal class OrganizationStatusReader(OrganizationAccessDbContext dbContext) : IOrganizationStatusReader
{
    public Task<OrganizationStatusResult> GetStatusAsync(OrganizationId organizationId, CancellationToken? cancellationToken)
    {
        if(organizationId.Value == Guid.Empty)
        {
            return Task.FromResult(OrganizationStatusResult.NotFound);
        }
        var organization = dbContext.Organizations.FirstOrDefault(o => o.Id == organizationId);
        if(organization is null)
        {
            return Task.FromResult(OrganizationStatusResult.NotFound);
        }
        if(organization.Status == OrganizationStatus.Active)
        {
            return Task.FromResult(OrganizationStatusResult.Active);
        }
        if(organization.Status == OrganizationStatus.Archived)
        {
            return Task.FromResult(OrganizationStatusResult.Archived);
        }
        throw new InvalidOperationException($"Unexpected organization status: {organization.Status}");
    }
}
