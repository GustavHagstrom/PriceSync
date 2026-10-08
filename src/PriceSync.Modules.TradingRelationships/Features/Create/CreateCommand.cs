using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.TradingRelationships.Features.Create;

internal record class CreateCommand(OrganizationId SupplierOrganizationId, OrganizationId CustomerOrganizationId);
