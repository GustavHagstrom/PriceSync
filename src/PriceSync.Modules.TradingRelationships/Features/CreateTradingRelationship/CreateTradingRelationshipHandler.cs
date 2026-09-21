using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.TradingRelationships.Features.CreateTradingRelationship;

internal class CreateTradingRelationshipHandler
{
    private readonly ICurrentPerson _currentPerson;
    private readonly IOrganizationAuthorizer _organizationAuthorizer;
    private readonly IOrganizationStatusReader _organizationStatusReader;
    private readonly TradingRelationshipsDbContext _dbContext;
    private readonly ICurrentOrganization _currentOrganization;

    internal CreateTradingRelationshipHandler(
        ICurrentPerson currentPerson, 
        ICurrentOrganization currentOrganization, 
        IOrganizationAuthorizer organizationAuthorizer, 
        IOrganizationStatusReader organizationStatusReader,
        TradingRelationshipsDbContext dbContext)
    {
        _currentPerson = currentPerson;
        _currentOrganization = currentOrganization;
        _organizationAuthorizer = organizationAuthorizer;
        _organizationStatusReader = organizationStatusReader;
        _dbContext = dbContext;
    }

    internal async Task<CreateTradingRelationshipResult> HandleAsync(CreateTradingRelationshipCommand command, CancellationToken cancellationToken)
    {
        if(_currentOrganization.OrganizationId != command.SupplierOrganizationId && _currentOrganization.OrganizationId != command.CustomerOrganizationId)
        {
            return new CreateTradingRelationshipResult.Forbidden();
        }

        var hasCapability = await _organizationAuthorizer.HasCapabilityAsync(_currentPerson.PersonId, _currentOrganization.OrganizationId, OrganizationCapability.ManageTradingRelationships, cancellationToken);
        if (!hasCapability)
        {
            return new CreateTradingRelationshipResult.Forbidden();
        }

        var supplierStatus = await _organizationStatusReader.GetStatusAsync(command.SupplierOrganizationId, cancellationToken);
        if(supplierStatus != OrganizationStatusResult.Active)
        {
            return new CreateTradingRelationshipResult.SupplierOrganizationArchived();
        }
        if(supplierStatus == OrganizationStatusResult.Archived)
        {
            return new CreateTradingRelationshipResult.SupplierNotFound();
        }

        var customerStatus = await _organizationStatusReader.GetStatusAsync(command.CustomerOrganizationId, cancellationToken);
        if (customerStatus != OrganizationStatusResult.Active)
        {
            return new CreateTradingRelationshipResult.CustomerOrganizationArchived();
        }
        if (customerStatus == OrganizationStatusResult.NotFound)
        {
            return new CreateTradingRelationshipResult.CustomerNotFound();
        }

        var 

        // Implementation for creating a trading relationship goes here.
        // For now, just return a new GUID as a placeholder.
        return new CreateTradingRelationshipResult();
    }
}
