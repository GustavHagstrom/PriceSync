using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Models;
using PriceSync.Modules.TradingRelationships.Persistence;


namespace PriceSync.Modules.TradingRelationships.Features.ListSupplier;

internal class ListSupplierHandler
{
    private const int MaxPageSize = 50;
    private readonly ICurrentPerson _currentPerson;
    private readonly ICurrentOrganization _currentOrganization;
    private readonly IOrganizationAuthorizer _organizationAuthorizer;
    private readonly TradingRelationshipsDbContext _dbContext;
    private readonly ILogger<ListSupplierHandler> _logger;

    public ListSupplierHandler(
        ICurrentPerson currentPerson, 
        ICurrentOrganization currentOrganization, 
        IOrganizationAuthorizer organizationAuthorizer,
        TradingRelationshipsDbContext dbContext,
        ILogger<ListSupplierHandler> logger)
    {
        _currentPerson = currentPerson;
        _currentOrganization = currentOrganization;
        _organizationAuthorizer = organizationAuthorizer;
        _dbContext = dbContext;
        _logger = logger;
    }
    public async Task<ListSupplierResult> HandleAsync(ListSupplierQuery query, CancellationToken cancellationToken)
    {
        if(_currentOrganization.OrganizationId?.Value is null || _currentOrganization.OrganizationId.Value.Value == Guid.Empty)
        {
            LogOutcome("Denied", "Current organization is not set.");
            return new ListSupplierResult.ActingOrganizationMissing();
        }

        var isCapable = await _organizationAuthorizer.HasCapabilityAsync(_currentPerson.PersonId, _currentOrganization.OrganizationId.Value, OrganizationCapability.ReadTradingRelationships, cancellationToken);
        if(!isCapable)
        {
            LogOutcome("Denied", "Current person does not have capability to read trading relationships.");
            return new ListSupplierResult.NotCapable();
        }

        var dbQuery = _dbContext.TradingRelationships
            .Where(tr => tr.SupplierOrganizationId == _currentOrganization.OrganizationId.Value);
        if (query.Filter == Models.ListStatusFilter.Active)
            dbQuery = dbQuery.Where(tr => tr.Status == TradingRelationshipStatus.Active);
        else if (query.Filter == Models.ListStatusFilter.Ended)
            dbQuery = dbQuery.Where(tr => tr.Status == TradingRelationshipStatus.Ended);
        else if (query.Filter == Models.ListStatusFilter.All) { }
        else
        {
            //this should not be possible. Validation should have caught this before it got here. But just in case, we will log and throw an exception.
            LogOutcome("Denied", "Invalid filter value.");
            throw new ArgumentOutOfRangeException(nameof(query.Filter), "Invalid filter value.");
        }

        if (query.Keyset is { } keyset)
        {
            var createdAt = keyset.CreatedAt;
            var id = keyset.Id;

            dbQuery = dbQuery.Where(tr => EF.Functions.LessThan(
                ValueTuple.Create(tr.CreatedAt, tr.Id),
                ValueTuple.Create(createdAt, id)));
        }

        var tradingRelationships = await dbQuery.OrderByDescending(tr => tr.CreatedAt)
            .ThenByDescending(tr => tr.Id)
            .Take(MaxPageSize + 1)
            .Select(tr => new TradingRelationshipResponse(tr))
            .ToListAsync(cancellationToken);

        if(tradingRelationships.Count > MaxPageSize)
        {
            tradingRelationships.RemoveAt(MaxPageSize);
            var nextKeyset = new ListKeyset(tradingRelationships[^1].CreatedAt, TradingRelationshipId.FromGuid(tradingRelationships[^1].Id));
            
            LogOutcome("Success", "Returned paginated results.");
            return new ListSupplierResult.Success(new ListSupplierResponse(tradingRelationships, nextKeyset.Cursor));
        }
        else
        {
            LogOutcome("Success", "Returned all results.");
            return new ListSupplierResult.Success(new ListSupplierResponse(tradingRelationships, null));
        }


    }
    private void LogOutcome(string outcome, string reason) => _logger.LogInformation("ListSupplier completed with {Outcome}, reason {Reason}", outcome, reason);
}
