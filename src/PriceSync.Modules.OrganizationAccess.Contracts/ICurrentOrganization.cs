namespace PriceSync.Modules.OrganizationAccess.Contracts;

public interface ICurrentOrganization
{
    OrganizationId? OrganizationId { get; }
}
