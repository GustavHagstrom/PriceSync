using PriceSync.Modules.TradingRelationships.Domain;

namespace PriceSync.Modules.TradingRelationships.Models;

internal sealed record class TradingRelationshipResponse
{
    public TradingRelationshipResponse(TradingRelationship tradingRelationship)
    {
        Id = tradingRelationship.Id.Value;
        SupplierId = tradingRelationship.SupplierOrganizationId.Value;
        CustomerId = tradingRelationship.CustomerOrganizationId.Value;
        CreatedAt = tradingRelationship.CreatedAt;
        EndedAt = tradingRelationship.EndedAt;
        var status = tradingRelationship.Status switch
        {
            TradingRelationshipStatus.Active => "active",
            TradingRelationshipStatus.Ended => "ended",
            _ => throw new ArgumentOutOfRangeException(nameof(tradingRelationship.Status), tradingRelationship.Status, null)
        };
        Status = status;
    }

    public Guid Id { get; }
    public Guid SupplierId { get; }
    public Guid CustomerId { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? EndedAt { get; }
    public string Status { get; }
}