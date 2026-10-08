namespace PriceSync.Modules.TradingRelationships.Features.Create;

internal sealed record CreateRequest(
    Guid SupplierOrganizationId,
    Guid CustomerOrganizationId);
