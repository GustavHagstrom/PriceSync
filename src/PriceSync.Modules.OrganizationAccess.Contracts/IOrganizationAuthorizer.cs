namespace PriceSync.Modules.OrganizationAccess.Contracts;

public interface IOrganizationAuthorizer
{
    Task<bool> HasCapabilityAsync(PersonId personId, OrganizationId organizationId, OrganizationCapability capability, CancellationToken? cancellationToken);
}
