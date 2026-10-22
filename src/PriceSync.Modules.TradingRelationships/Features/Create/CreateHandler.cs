using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.TradingRelationships.Infrastructure;
using Npgsql;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Models;
using PriceSync.Modules.TradingRelationships.Persistence;
using PriceSync.Modules.TradingRelationships.Persistence.Configurations;

namespace PriceSync.Modules.TradingRelationships.Features.Create;

internal class CreateHandler
{
    private readonly ILogger<CreateHandler> _logger;
    private readonly ICurrentPerson _currentPerson;
    private readonly IOrganizationAuthorizer _organizationAuthorizer;
    private readonly IOrganizationStatusReader _organizationStatusReader;
    private readonly TimeProvider _timeProvider;
    private readonly TradingRelationshipsDbContext _dbContext;
    private readonly ICurrentOrganization _currentOrganization;

    public CreateHandler(
        ICurrentPerson currentPerson, 
        ICurrentOrganization currentOrganization, 
        IOrganizationAuthorizer organizationAuthorizer, 
        IOrganizationStatusReader organizationStatusReader,
        TimeProvider timeProvider,
        TradingRelationshipsDbContext dbContext,
        ILogger<CreateHandler> logger)
    {
        _logger = logger;
        _currentPerson = currentPerson;
        _currentOrganization = currentOrganization;
        _organizationAuthorizer = organizationAuthorizer;
        _organizationStatusReader = organizationStatusReader;
        _timeProvider = timeProvider;
        _dbContext = dbContext;
    }

    internal async Task<CreateResult> HandleAsync(CreateCommand command, CancellationToken cancellationToken)
    {
        using var scope = LogScope.BeginScope(_logger, "CreateTradingRelationship",
            personId: _currentPerson.PersonId.Value, organizationId: _currentOrganization.OrganizationId?.Value);
        using var pairScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["SupplierOrganizationId"] = command.SupplierOrganizationId.Value,
            ["CustomerOrganizationId"] = command.CustomerOrganizationId.Value,
        });
        try
        {
            var result = await CreateAsync(command, cancellationToken);
            var outcome = result is CreateResult.Created or CreateResult.Existed ? "Succeeded" : "Denied";
            _logger.LogInformation(
                "CreateTradingRelationship completed with {Outcome}, reason {Reason}",
                outcome, result.GetType().Name);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "CreateTradingRelationship completed with {Outcome}, reason {Reason}",
                "Cancelled", "RequestCancelled");
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "CreateTradingRelationship completed with {Outcome}, reason {Reason}, error type {ErrorType}",
                "Failed", "TechnicalFailure", exception.GetType().FullName);
            throw;
        }
    }

    private async Task<CreateResult> CreateAsync(CreateCommand command, CancellationToken cancellationToken)
    {
        if(_currentOrganization.OrganizationId is null)
        {
            return new CreateResult.ActingOrganizationRequired();
        }

        if (_currentOrganization.OrganizationId != command.SupplierOrganizationId && _currentOrganization.OrganizationId != command.CustomerOrganizationId)
        {
            return new CreateResult.Forbidden();
        }

        var hasCapability = await _organizationAuthorizer.HasCapabilityAsync(_currentPerson.PersonId, _currentOrganization.OrganizationId.Value, OrganizationCapability.ManageTradingRelationships, cancellationToken);
        if (!hasCapability)
        {
            return new CreateResult.Forbidden();
        }

        var supplierStatus = await _organizationStatusReader.GetStatusAsync(command.SupplierOrganizationId, cancellationToken);
        if(supplierStatus == OrganizationStatusResult.Archived)
        {
            return new CreateResult.SupplierOrganizationEnded();
        }
        if(supplierStatus == OrganizationStatusResult.NotFound)
        {
            return new CreateResult.SupplierNotFound();
        }

        var customerStatus = await _organizationStatusReader.GetStatusAsync(command.CustomerOrganizationId, cancellationToken);
        if (customerStatus == OrganizationStatusResult.Archived)
        {
            return new CreateResult.CustomerOrganizationEnded();
        }
        if (customerStatus == OrganizationStatusResult.NotFound)
        {
            return new CreateResult.CustomerNotFound();
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
            return new CreateResult.Existed(new TradingRelationshipResponse(existingRelationship));
        }

        try
        {
            _dbContext.TradingRelationships.Add(tradingRelationship);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new CreateResult.Created(new TradingRelationshipResponse(tradingRelationship));
        }
        catch (DbUpdateException ex) when (IsActiveRelationshipUniqueViolation(ex))
        {
            _dbContext.ChangeTracker.Clear();

            // End may have completed since the unique-index conflict. Prefer the current
            // active relationship, otherwise return the latest history for this pair.
            var winner = await _dbContext.TradingRelationships.AsNoTracking()
                .Where(x =>
                    x.SupplierOrganizationId == command.SupplierOrganizationId &&
                    x.CustomerOrganizationId == command.CustomerOrganizationId)
                .OrderByDescending(x => x.Status == TradingRelationshipStatus.Active)
                .ThenByDescending(x => x.EndedAt)
                .ThenByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .FirstAsync(cancellationToken);

            return new CreateResult.Existed(new TradingRelationshipResponse(winner));
        }
    }
    private bool IsActiveRelationshipUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException postgresException &&
               postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
               postgresException.ConstraintName == TradingRelationshipConfiguration.ActiveSupplierCustomerUniqueIndex;
    }
}
