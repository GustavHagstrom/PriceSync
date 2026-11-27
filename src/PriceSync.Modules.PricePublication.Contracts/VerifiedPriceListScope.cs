using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.PricePublication.Contracts;

public sealed record VerifiedPriceListScope(PriceListId PriceListId, OrganizationId OwnerOrganizationId,
    PriceListType Type, string Currency, string Market, OrganizationId? CustomerOrganizationId);
