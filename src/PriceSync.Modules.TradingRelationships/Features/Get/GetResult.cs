using Microsoft.EntityFrameworkCore.Diagnostics;
using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.Get;

internal abstract record GetResult
{
    public sealed record Found(TradingRelationshipResponse Response) : GetResult;
    public sealed record NotFound() : GetResult;
    public sealed record NotCapable() : GetResult;
    public sealed record OrganizationMissing() : GetResult; 
}
