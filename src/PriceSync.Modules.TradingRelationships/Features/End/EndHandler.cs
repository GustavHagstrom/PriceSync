using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Models;
using PriceSync.Modules.TradingRelationships.Persistence;
using System.Diagnostics;

namespace PriceSync.Modules.TradingRelationships.Features.End;

internal class EndHandler
{
    private readonly ICurrentPerson _currentPerson;
    private readonly ICurrentOrganization _currentOrganization;
    private readonly IOrganizationAuthorizer _organizationAuthorizer;
    private readonly IOrganizationStatusReader _organizationStatusReader;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EndHandler> _logger;
    private readonly TradingRelationshipsDbContext _dbContext;

    public EndHandler(
        ICurrentPerson currentPerson,
        ICurrentOrganization currentOrganization,
        IOrganizationAuthorizer organizationAuthorizer,
        IOrganizationStatusReader organizationStatusReader,
        TimeProvider timeProvider,
        ILogger<EndHandler> logger,
        TradingRelationshipsDbContext dbContext)
    {
        _currentPerson = currentPerson;
        _currentOrganization = currentOrganization;
        _organizationAuthorizer = organizationAuthorizer;
        _organizationStatusReader = organizationStatusReader;
        _timeProvider = timeProvider;
        _logger = logger;
        _dbContext = dbContext;
    }
    internal async Task<EndResult> HandleAsync(EndCommand command, CancellationToken cancellationToken)
    {
        try
        {
            if (_currentOrganization.OrganizationId is null || _currentOrganization.OrganizationId.Value.Value == Guid.Empty)
            {
                LogOutcome("Denied", "MissingActingOrganization");
                return new EndResult.ActingOrganizationRequired();
            }
            var hasCapability = await _organizationAuthorizer.HasCapabilityAsync(_currentPerson.PersonId, _currentOrganization.OrganizationId!.Value, OrganizationCapability.ManageTradingRelationships, cancellationToken);
            if (!hasCapability)
            {
                LogOutcome("Denied", "AuthorizationDenied");
                return new EndResult.Forbidden();
            }
            var organizationStatus = await _organizationStatusReader.GetStatusAsync(_currentOrganization.OrganizationId.Value, cancellationToken);
            if (organizationStatus == OrganizationStatusResult.Archived)
            {
                LogOutcome("Denied", "ActingOrganizationArchived");
                return new EndResult.Forbidden();
            }
            if (organizationStatus == OrganizationStatusResult.NotFound)
            {
                LogOutcome("Denied", "ActingOrganizationNotFound");
                return new EndResult.Forbidden();
            }
            var actingOrganizationId = _currentOrganization.OrganizationId.Value;
            var scopedRelationships = _dbContext.TradingRelationships.Where(x =>
                x.Id == command.TradingRelationshipId &&
                (x.SupplierOrganizationId == actingOrganizationId || x.CustomerOrganizationId == actingOrganizationId));
            var tradingRelationship = await scopedRelationships.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (tradingRelationship is null)
            {
                LogOutcome("Denied", "TradingRelationshipNotFound");
                return new EndResult.TradingRelationshipNotFound();
            }
            if (tradingRelationship.Status == TradingRelationshipStatus.Ended)
            {
                LogOutcome("Succeeded", "AlreadyEnded", tradingRelationship.EndedAt);
                return new EndResult.Ended(new TradingRelationshipResponse(tradingRelationship));
            }

            var now = _timeProvider.GetUtcNow();

            var affectedRows = await scopedRelationships
                .Where(x =>
                    x.EndedAt == null &&
                    x.Status == TradingRelationshipStatus.Active)
                .ExecuteUpdateAsync(
                    x =>
                        x.SetProperty(tr => tr.Status, TradingRelationshipStatus.Ended)
                        .SetProperty(tr => tr.EndedAt, now)
                    ,cancellationToken);


            tradingRelationship = await scopedRelationships.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
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

            LogOutcome("Succeeded", affectedRows == 1 ? "Ended" : "AlreadyEnded", tradingRelationship.EndedAt);
            return new EndResult.Ended(new TradingRelationshipResponse(tradingRelationship));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogOutcome("Cancelled", "RequestCancelled");
            throw;
        }
        catch (Exception exception)
        {
            // The host logs the exception; this entry records the operation's audit context.
            _logger.LogError(
                "EndTradingRelationship completed with {Outcome}, reason {Reason}, error type {ErrorType}",
                "Failed", "TechnicalFailure", exception.GetType().FullName);
            throw;
        }
    }

    private void LogOutcome(string outcome, string reason, DateTimeOffset? endedAt = null) =>
        _logger.LogInformation(
            "EndTradingRelationship completed with {Outcome}, reason {Reason}, EndedAt {EndedAt}",
            outcome, reason, endedAt);
}
