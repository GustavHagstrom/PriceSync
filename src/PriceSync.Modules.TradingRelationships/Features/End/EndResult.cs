using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.End;

internal abstract record EndResult
{
    internal sealed record Ended(TradingRelationshipResponse Response) : EndResult;
    internal sealed record TradingRelationshipNotFound : EndResult;
    internal sealed record ActingOrganizationRequired : EndResult;
    internal sealed record ActingOrganizationEnded : EndResult;
    internal sealed record ActingOrganizationNotFound : EndResult;
    internal sealed record ActingOrganizationNotMemberOfTradingRelationship : EndResult;
    internal sealed record Forbidden : EndResult;
}
