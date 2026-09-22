using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Models;
using PriceSync.Modules.TradingRelationships.Persistence;
using PriceSync.Modules.TradingRelationships.Persistence.Configurations;

namespace PriceSync.Modules.TradingRelationships.Features.CreateTradingRelationship;

internal class CreateTradingRelationshipHandler
{
    private readonly ICurrentPerson _currentPerson;
    private readonly IOrganizationAuthorizer _organizationAuthorizer;
    private readonly IOrganizationStatusReader _organizationStatusReader;
    private readonly TimeProvider _timeProvider;
    private readonly TradingRelationshipsDbContext _dbContext;
    private readonly ICurrentOrganization _currentOrganization;

    public CreateTradingRelationshipHandler(
        ICurrentPerson currentPerson, 
        ICurrentOrganization currentOrganization, 
        IOrganizationAuthorizer organizationAuthorizer, 
        IOrganizationStatusReader organizationStatusReader,
        TimeProvider timeProvider,
        TradingRelationshipsDbContext dbContext)
    {
        _currentPerson = currentPerson;
        _currentOrganization = currentOrganization;
        _organizationAuthorizer = organizationAuthorizer;
        _organizationStatusReader = organizationStatusReader;
        _timeProvider = timeProvider;
        _dbContext = dbContext;
    }

    internal async Task<CreateTradingRelationshipResult> HandleAsync(CreateTradingRelationshipCommand command, CancellationToken cancellationToken)
    {
        if(_currentOrganization.OrganizationId is null)
        {
            return new CreateTradingRelationshipResult.ActingOrganizationRequired();
        }

        if (_currentOrganization.OrganizationId != command.SupplierOrganizationId && _currentOrganization.OrganizationId != command.CustomerOrganizationId)
        {
            return new CreateTradingRelationshipResult.Forbidden();
        }

        var hasCapability = await _organizationAuthorizer.HasCapabilityAsync(_currentPerson.PersonId, _currentOrganization.OrganizationId.Value, OrganizationCapability.ManageTradingRelationships, cancellationToken);
        if (!hasCapability)
        {
            return new CreateTradingRelationshipResult.Forbidden();
        }

        var supplierStatus = await _organizationStatusReader.GetStatusAsync(command.SupplierOrganizationId, cancellationToken);
        if(supplierStatus == OrganizationStatusResult.Archived)
        {
            return new CreateTradingRelationshipResult.SupplierOrganizationEnded();
        }
        if(supplierStatus == OrganizationStatusResult.NotFound)
        {
            return new CreateTradingRelationshipResult.SupplierNotFound();
        }

        var customerStatus = await _organizationStatusReader.GetStatusAsync(command.CustomerOrganizationId, cancellationToken);
        if (customerStatus == OrganizationStatusResult.Archived)
        {
            return new CreateTradingRelationshipResult.CustomerOrganizationEnded();
        }
        if (customerStatus == OrganizationStatusResult.NotFound)
        {
            return new CreateTradingRelationshipResult.CustomerNotFound();
        }


        var tradingRelationship = TradingRelationship.Create(command.SupplierOrganizationId, command.CustomerOrganizationId, _timeProvider.GetUtcNow());

        var existingRelationship = await _dbContext.TradingRelationships
            .FirstOrDefaultAsync(x =>
                x.SupplierOrganizationId == command.SupplierOrganizationId &&
                x.CustomerOrganizationId == command.CustomerOrganizationId &&
                x.Status == TradingRelationshipStatus.Active,
                cancellationToken);

        if(existingRelationship != null)
        {
            return new CreateTradingRelationshipResult.Existed(new TradingRelationshipResponse(existingRelationship));
        }

        try
        {
            _dbContext.TradingRelationships.Add(tradingRelationship);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new CreateTradingRelationshipResult.Created(new TradingRelationshipResponse(tradingRelationship));
        }
        catch (DbUpdateException ex) when (IsActiveRelationshipUniqueViolation(ex))
        {
            _dbContext.ChangeTracker.Clear();

            var winner = await _dbContext.TradingRelationships
                .SingleAsync(x =>
                    x.SupplierOrganizationId == command.SupplierOrganizationId &&
                    x.CustomerOrganizationId == command.CustomerOrganizationId &&
                    x.Status == TradingRelationshipStatus.Active,
                    cancellationToken);

            return new CreateTradingRelationshipResult.Existed(new TradingRelationshipResponse(winner));
        }
    }
    private bool IsActiveRelationshipUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException postgresException &&
               postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
               postgresException.ConstraintName == TradingRelationshipConfiguration.ActiveSupplierCustomerUniqueIndex;
    }
}
