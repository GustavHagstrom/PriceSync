using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.TradingRelationships.Features.Create;
using PriceSync.Modules.TradingRelationships.Features.End;

namespace PriceSync.Modules.TradingRelationships;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapTradingRelationshipsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints
            .MapGroup(Routes.BaseV1)
            .RequireAuthorization();

        api.MapPost("", CreateEndpoint.HandleAsync).WithName(Routes.Names.Create);
        api.MapDelete("{tradingRelationshipId:guid}", EndEndpoint.HandleAsync).WithName(Routes.Names.End);

        return endpoints;
    }
}
