using System.Text.Json.Serialization;

namespace PriceSync.Modules.TradingRelationships.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ListStatusFilter>))]
internal enum ListStatusFilter
{
    Active,
    Ended,
    All
}
