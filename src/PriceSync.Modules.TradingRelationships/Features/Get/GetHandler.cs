using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Models;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.TradingRelationships.Features.Get;

internal class GetHandler
{
    private readonly ICurrentPerson _currentPerson;
    private readonly ICurrentOrganization _currentOrganization;
    private readonly TradingRelationshipsDbContext _dbContext;
    private readonly IOrganizationAuthorizer _organizationAuthorizer;
    private readonly IOrganizationStatusReader _organizationStatusReader;
    private readonly ILogger<GetHandler> _logger;

    public GetHandler(
        ICurrentPerson currentPerson, 
        ICurrentOrganization currentOrganization, 
        TradingRelationshipsDbContext dbContext, 
        IOrganizationAuthorizer organizationAuthorizer,
        IOrganizationStatusReader organizationStatusReader,
        ILogger<GetHandler> logger)
    {
        _currentPerson = currentPerson;
        _currentOrganization = currentOrganization;
        _dbContext = dbContext;
        _organizationAuthorizer = organizationAuthorizer;
        _organizationStatusReader = organizationStatusReader;
        _logger = logger;
    }
    public async Task<GetResult> HandleAsync(TradingRelationshipId tradingRelationshipId, CancellationToken cancellationToken)
    {
        if (_currentOrganization.OrganizationId is null || _currentOrganization.OrganizationId.Value.Value == Guid.Empty)
        {
            LogOutcome("Denied", "MissingActingOrganization");
            return new GetResult.OrganizationMissing();
        }
        var organizationStatus = await _organizationStatusReader.GetStatusAsync(_currentOrganization.OrganizationId.Value, cancellationToken);
        if (organizationStatus == OrganizationStatusResult.NotFound)
        {
            LogOutcome("Denied", "OrganizationNotFound");
            return new GetResult.OrganizationMissing();
        }
        var tradingRelationship = await _dbContext.TradingRelationships.AsNoTracking().SingleOrDefaultAsync(tr => tr.Id == tradingRelationshipId, cancellationToken);
        if (tradingRelationship is null)
        {
            LogOutcome("Denied", "NotFound");
            return new GetResult.NotFound();
        }
        if(tradingRelationship.SupplierOrganizationId != _currentOrganization.OrganizationId.Value && tradingRelationship.CustomerOrganizationId != _currentOrganization.OrganizationId.Value)
        {
            LogOutcome("Denied", "ActingOrganizationNotPartOfTradingRelationship");
            return new GetResult.NotFound();
        }
        var hasCapability = await _organizationAuthorizer.HasCapabilityAsync(_currentPerson.PersonId, _currentOrganization.OrganizationId.Value, OrganizationCapability.ReadTradingRelationships, cancellationToken);
        if (!hasCapability)
        {
            LogOutcome("Denied", "Unauthorized");
            return new GetResult.NotCapable();
        }
        return new GetResult.Found(new TradingRelationshipResponse(tradingRelationship));
    }
    private void LogOutcome(string outcome, string reason) =>
        _logger.LogInformation(
            "EndTradingRelationship completed with {Outcome}, reason {Reason}",
            outcome, reason);
}
