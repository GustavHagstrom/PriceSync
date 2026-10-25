using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.PricePublication.Persistence;

namespace PriceSync.Modules.PricePublication.Features.Verification;

internal sealed class PriceListScopeVerifier(PricePublicationDbContext db, ILogger<PriceListScopeVerifier> logger) : IPriceListScopeVerifier
{
    public async Task<PriceListVerificationResult> VerifyAsync(OrganizationId supplier, OrganizationId customer,
        IReadOnlyCollection<PriceListReference> references, CancellationToken cancellationToken = default)
    {
        try
        {
            return await VerifyCoreAsync(supplier, customer, references, cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or TimeoutException ||
            exception is InvalidOperationException { InnerException: DbException or TimeoutException })
        {
            logger.LogWarning("Price List scope verification unavailable for Supplier {SupplierOrganizationId}, error type {ErrorType}",
                supplier.Value, exception.GetType().FullName);
            return new PriceListVerificationResult.Unavailable();
        }
    }

    private async Task<PriceListVerificationResult> VerifyCoreAsync(OrganizationId supplier, OrganizationId customer,
        IReadOnlyCollection<PriceListReference> references, CancellationToken cancellationToken)
    {
        if (supplier.Value == Guid.Empty || customer.Value == Guid.Empty)
            throw new ArgumentException("Supplier and Customer IDs must be nonempty.");
        ArgumentNullException.ThrowIfNull(references);
        var requested = references.ToArray();
        var ids = requested.Select(x => x.PriceListId).Distinct().ToArray();
        var lists = await db.PriceLists.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var scopes = new List<VerifiedPriceListScope>();
        var invalid = new List<InvalidPriceListReference>();
        foreach (var reference in requested)
        {
            if (reference.PriceListId.Value == Guid.Empty)
            {
                invalid.Add(new(reference.PriceListId, InvalidPriceListReferenceReason.InvalidId));
                continue;
            }
            if (!lists.TryGetValue(reference.PriceListId, out var list))
            {
                invalid.Add(new(reference.PriceListId, InvalidPriceListReferenceReason.NotFound));
                continue;
            }
            var type = list.Type switch
            {
                "general" => PriceListType.General,
                "customerSpecificNet" => PriceListType.CustomerSpecificNet,
                _ => throw new InvalidOperationException("Persisted Price List type is unsupported."),
            };
            if (list.OwnerOrganizationId != supplier)
                invalid.Add(new(reference.PriceListId, InvalidPriceListReferenceReason.WrongSupplier));
            else if (type != reference.ExpectedType)
                invalid.Add(new(reference.PriceListId, InvalidPriceListReferenceReason.IncompatibleType));
            else if (list.CustomerOrganizationId is { } binding && binding != customer)
                invalid.Add(new(reference.PriceListId, InvalidPriceListReferenceReason.WrongCustomer));
            else
                scopes.Add(new(list.Id, list.OwnerOrganizationId, type, list.Currency, list.Market, list.CustomerOrganizationId));
        }
        return invalid.Count > 0
            ? new PriceListVerificationResult.Invalid(invalid.AsReadOnly())
            : new PriceListVerificationResult.Verified(scopes.AsReadOnly());
    }
}
