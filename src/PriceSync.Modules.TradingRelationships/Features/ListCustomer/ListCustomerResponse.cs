using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.ListCustomer;

internal record class ListCustomerResponse(IReadOnlyList<TradingRelationshipResponse> TradingRelationships, string? Cursor);
