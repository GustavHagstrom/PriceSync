using System.Text.Json.Serialization;

namespace PriceSync.Modules.TradingRelationships.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ListStatusFilter>))]
public enum ListStatusFilter
{
    Active,
    Ended,
    All
}
