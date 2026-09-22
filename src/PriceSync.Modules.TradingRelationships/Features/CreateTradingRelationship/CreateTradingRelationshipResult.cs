using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;

namespace PriceSync.Modules.TradingRelationships.Features.CreateTradingRelationship;

internal abstract record class CreateTradingRelationshipResult
{
    internal sealed record Created(TradingRelationshipId Id, OrganizationId SupplierId, OrganizationId CustomerId, DateTimeOffset CreatedAt, LifecycleStatus Status) : CreateTradingRelationshipResult;
    internal sealed record Existed(TradingRelationshipId Id, OrganizationId SupplierId, OrganizationId CustomerId, DateTimeOffset CreatedAt, LifecycleStatus Status) : CreateTradingRelationshipResult;
    internal sealed record Forbidden : CreateTradingRelationshipResult;
    internal sealed record MissingCurrentOrganization : CreateTradingRelationshipResult;
    internal sealed record SupplierNotFound : CreateTradingRelationshipResult;
    internal sealed record CustomerNotFound : CreateTradingRelationshipResult;
    internal sealed record SupplierOrganizationEnded : CreateTradingRelationshipResult;
    internal sealed record CustomerOrganizationEnded : CreateTradingRelationshipResult;
}
