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
    public TradingRelationshipStatus Status { get; private set; }

    private TradingRelationship() { }

    internal void End(DateTimeOffset now)
    {
        if (Status == TradingRelationshipStatus.Ended)
        {
            throw new InvalidOperationException("Trading relationship is already ended.");
        }
        Status = TradingRelationshipStatus.Ended;
        EndedAt = now;
    }
    internal static TradingRelationship Create(OrganizationId supplierOrganizationId, OrganizationId customerOrganizationId, DateTimeOffset now)
    {
        if (supplierOrganizationId.Value == Guid.Empty) throw new ArgumentException("Supplier organization ID cannot be empty.", nameof(supplierOrganizationId));
        if (customerOrganizationId.Value == Guid.Empty) throw new ArgumentException("Customer organization ID cannot be empty.", nameof(customerOrganizationId));
        if(supplierOrganizationId == customerOrganizationId) throw new ArgumentException("Supplier and customer organization IDs cannot be the same.");
        return new TradingRelationship
        {
            Id = TradingRelationshipId.Create(),
            SupplierOrganizationId = supplierOrganizationId,
            CustomerOrganizationId = customerOrganizationId,
            CreatedAt = now,
            Status = TradingRelationshipStatus.Active
        };
    }
}
