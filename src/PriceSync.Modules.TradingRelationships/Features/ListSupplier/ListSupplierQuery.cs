using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.ListSupplier;

internal record class ListSupplierQuery(ListStatusFilter Filter, ListKeyset? Keyset);