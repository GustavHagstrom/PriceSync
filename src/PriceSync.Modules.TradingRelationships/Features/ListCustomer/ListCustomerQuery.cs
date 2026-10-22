using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.ListCustomer;

internal record class ListCustomerQuery(ListStatusFilter Filter, ListKeyset? Keyset);