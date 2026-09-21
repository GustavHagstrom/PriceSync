namespace PriceSync.Modules.OrganizationAccess.Contracts;

public interface IOrganizationStatusReader
{
    Task<OrganizationStatusResult> GetStatusAsync(OrganizationId organizationId, CancellationToken? cancellationToken);
}

public enum OrganizationStatusResult
{
    Active,
    Archived,
    NotFound,
}