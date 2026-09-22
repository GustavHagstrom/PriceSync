using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.TradingRelationships.Features.CreateTradingRelationship;

namespace PriceSync.Modules.TradingRelationships;

public static class TradingRelationshipsEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTradingRelationshipsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints
            .MapGroup("/api/v1/trading-relationships")
            .RequireAuthorization();

        api.MapPost("", CreateTradingRelationshipEndpoint.HandleAsync)
            .WithName(CreateTradingRelationshipEndpoint.RouteName);


        return endpoints;
    }
}
