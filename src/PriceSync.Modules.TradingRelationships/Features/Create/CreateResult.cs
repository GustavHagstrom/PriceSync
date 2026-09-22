using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.Create;

internal abstract record class CreateResult
{
    internal sealed record Created(TradingRelationshipResponse Response) : CreateResult;
    internal sealed record Existed(TradingRelationshipResponse Response) : CreateResult;
    internal sealed record Forbidden : CreateResult;
    internal sealed record ActingOrganizationRequired : CreateResult;
    internal sealed record SupplierNotFound : CreateResult;
    internal sealed record CustomerNotFound : CreateResult;
    internal sealed record SupplierOrganizationEnded : CreateResult;
    internal sealed record CustomerOrganizationEnded : CreateResult;
}
