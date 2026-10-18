using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.ListSupplier;

internal record class ListSupplierResponse(IReadOnlyList<TradingRelationshipResponse> TradingRelationships, string? cursor);
