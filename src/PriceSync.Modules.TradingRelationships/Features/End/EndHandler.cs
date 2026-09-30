using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Models;
using PriceSync.Modules.TradingRelationships.Persistence;
using System.Data;

namespace PriceSync.Modules.TradingRelationships.Features.End;

internal class EndHandler
{
    private readonly ICurrentPerson _currentPerson;
    private readonly ICurrentOrganization _currentOrganization;
    private readonly IOrganizationAuthorizer _organizationAuthorizer;
    private readonly IOrganizationStatusReader _organizationStatusReader;
    private readonly TimeProvider _timeProvider;
    private readonly TradingRelationshipsDbContext _dbContext;

    public EndHandler(
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
    internal async Task<EndResult> HandleAsync(EndCommand command, CancellationToken cancellationToken)
    {
        if(_currentOrganization.OrganizationId is null || _currentOrganization.OrganizationId.Value.Value == Guid.Empty)
        {
            return new EndResult.ActingOrganizationRequired();
        }
        var hasCapability = await _organizationAuthorizer.HasCapabilityAsync(_currentPerson.PersonId, _currentOrganization.OrganizationId!.Value, OrganizationCapability.ManageTradingRelationships, cancellationToken);
        if (!hasCapability)
        {
            return new EndResult.Forbidden();
        }
        var organizationStatus = await _organizationStatusReader.GetStatusAsync(_currentOrganization.OrganizationId.Value, cancellationToken);
        if(organizationStatus == OrganizationStatusResult.Archived)
        {
            return new EndResult.ActingOrganizationEnded();
        }
        if(organizationStatus == OrganizationStatusResult.NotFound)
        {
            return new EndResult.ActingOrganizationNotFound();
        }
        var tradingRelationship = await _dbContext.TradingRelationships.SingleOrDefaultAsync(x => x.Id == command.TradingRelationshipId, cancellationToken);
        if(tradingRelationship is null)
        {
            return new EndResult.TradingRelationshipNotFound();
        }
        if(_currentOrganization.OrganizationId != tradingRelationship.CustomerOrganizationId && _currentOrganization.OrganizationId != tradingRelationship.SupplierOrganizationId)
        {
            return new EndResult.ActingOrganizationNotMemberOfTradingRelationship();
        }
        
        if(tradingRelationship.Status == TradingRelationshipStatus.Ended)
        {
            return new EndResult.Ended(new TradingRelationshipResponse(tradingRelationship));
        }

        tradingRelationship.End(_timeProvider.GetUtcNow());

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new EndResult.Ended(new TradingRelationshipResponse(tradingRelationship));
        }
        catch (DbUpdateConcurrencyException)
        {
            var winner = await _dbContext.TradingRelationships.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.TradingRelationshipId, cancellationToken);
            if(winner is null)
            {
                throw new Exception("Unexpected error: Trading relationship not found after concurrency exception.");
            }
            return new EndResult.Ended(new TradingRelationshipResponse(winner));
        }
        
    }
}