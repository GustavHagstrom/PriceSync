using System.Text.Json.Serialization;
using PriceSync.Modules.TradingRelationships.Domain;

namespace PriceSync.Modules.TradingRelationships.Models;

[method: JsonConstructor]
internal sealed record TradingRelationshipResponse(
    Guid Id,
    Guid SupplierId,
    Guid CustomerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndedAt,
    string Status)
{
    public TradingRelationshipResponse(TradingRelationship relationship)
        : this(
            relationship.Id.Value,
            relationship.SupplierOrganizationId.Value,
            relationship.CustomerOrganizationId.Value,
            relationship.CreatedAt,
            relationship.EndedAt,
            relationship.Status switch
            {
                TradingRelationshipStatus.Active => "active",
                TradingRelationshipStatus.Ended => "ended",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(relationship.Status),
                    relationship.Status,
                    null)
            })
    {
    }
}