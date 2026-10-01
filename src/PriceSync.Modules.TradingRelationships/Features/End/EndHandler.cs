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
        var tradingRelationship = await _dbContext.TradingRelationships.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.TradingRelationshipId, cancellationToken);
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

        var now = _timeProvider.GetUtcNow();

        var affectedRows = await _dbContext.TradingRelationships
            .Where(x => 
                x.Id == command.TradingRelationshipId && 
                x.EndedAt == null && 
                x.Status == TradingRelationshipStatus.Active)
            .ExecuteUpdateAsync(
                x => 
                    x.SetProperty(tr => tr.Status, TradingRelationshipStatus.Ended)
                    .SetProperty(tr => tr.EndedAt, now)
                ,cancellationToken);
        

        tradingRelationship = await _dbContext.TradingRelationships.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.TradingRelationshipId, cancellationToken);
        if (tradingRelationship is null)
        {
            throw new InvalidOperationException(
                "Trading relationship disappeared during End operation.");
        }

        if (tradingRelationship.Status != TradingRelationshipStatus.Ended)
        {
            throw new InvalidOperationException(
                $"Expected trading relationship to be Ended after End operation, " +
                $"but status was {tradingRelationship.Status}.");
        }

        return new EndResult.Ended(new TradingRelationshipResponse(tradingRelationship));

    }
}