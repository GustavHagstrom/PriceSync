using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.TradingRelationships.Domain;

internal class TradingRelationship
{
    public TradingRelationshipId Id { get; private set; }
    public OrganizationId SupplierOrganizationId { get; private set; }
    public OrganizationId CustomerOrganizationId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public LifecycleStatus Status { get; private set; }

    private TradingRelationship() { }

    internal void End(TimeProvider timeProvider)
    {
        if (timeProvider == null) throw new ArgumentNullException(nameof(timeProvider));
        if (Status == LifecycleStatus.Ended)
        {
            throw new InvalidOperationException("Trading relationship is already ended.");
        }
        Status = LifecycleStatus.Ended;
        EndedAt = timeProvider.GetUtcNow();
    }
    internal static TradingRelationship Create(TradingRelationshipId id, OrganizationId supplierOrganizationId, OrganizationId customerOrganizationId, TimeProvider timeProvider)
    {
        if(id.Value == Guid.Empty) throw new ArgumentException("Trading relationship ID cannot be empty.", nameof(id));
        if (supplierOrganizationId.Value == Guid.Empty) throw new ArgumentException("Supplier organization ID cannot be empty.", nameof(supplierOrganizationId));
        if (customerOrganizationId.Value == Guid.Empty) throw new ArgumentException("Customer organization ID cannot be empty.", nameof(customerOrganizationId));
        if(supplierOrganizationId == customerOrganizationId) throw new ArgumentException("Supplier and customer organization IDs cannot be the same.");
        if (timeProvider == null) throw new ArgumentNullException(nameof(timeProvider));
        return new TradingRelationship
        {
            Id = id,
            SupplierOrganizationId = supplierOrganizationId,
            CustomerOrganizationId = customerOrganizationId,
            CreatedAt = timeProvider.GetUtcNow(),
            Status = LifecycleStatus.Active
        };
    }
}
