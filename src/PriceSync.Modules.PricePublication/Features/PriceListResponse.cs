using PriceSync.Modules.PricePublication.Domain;

namespace PriceSync.Modules.PricePublication.Features;

internal sealed record PriceListResponse(Guid Id, string Name, Guid OwnerOrganizationId, string Type,
    string Currency, string Market, Guid? CustomerOrganizationId)
{
    internal static PriceListResponse From(PriceList list) => new(list.Id.Value, list.Name,
        list.OwnerOrganizationId.Value, list.Type, list.Currency, list.Market, list.CustomerOrganizationId?.Value);
}
