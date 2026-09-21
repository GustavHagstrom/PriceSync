namespace PriceSync.Modules.TradingRelationships.Features.CreateTradingRelationship;

internal abstract record class CreateTradingRelationshipResult
{
    internal sealed record Success(Guid TradingRelationshipId) : CreateTradingRelationshipResult;
    internal sealed record Forbidden : CreateTradingRelationshipResult;
    internal sealed record SupplierNotFound : CreateTradingRelationshipResult;
    internal sealed record CustomerNotFound : CreateTradingRelationshipResult;
    internal sealed record SupplierOrganizationArchived : CreateTradingRelationshipResult;
    internal sealed record CustomerOrganizationArchived : CreateTradingRelationshipResult;
}
