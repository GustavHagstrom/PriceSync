using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.Get;

internal abstract record GetResult
{
    public sealed record Found(TradingRelationshipResponse Response) : GetResult;
}
