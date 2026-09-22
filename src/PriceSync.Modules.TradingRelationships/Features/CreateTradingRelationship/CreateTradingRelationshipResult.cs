using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.CreateTradingRelationship;

internal abstract record class CreateTradingRelationshipResult
{
    internal sealed record Created(TradingRelationshipResponse Response) : CreateTradingRelationshipResult;
    internal sealed record Existed(TradingRelationshipResponse Response) : CreateTradingRelationshipResult;
    internal sealed record Forbidden : CreateTradingRelationshipResult;
    internal sealed record ActingOrganizationRequired : CreateTradingRelationshipResult;
    internal sealed record SupplierNotFound : CreateTradingRelationshipResult;
    internal sealed record CustomerNotFound : CreateTradingRelationshipResult;
    internal sealed record SupplierOrganizationEnded : CreateTradingRelationshipResult;
    internal sealed record CustomerOrganizationEnded : CreateTradingRelationshipResult;
}
