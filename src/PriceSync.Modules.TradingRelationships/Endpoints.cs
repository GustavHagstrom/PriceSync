using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.TradingRelationships.Features.Create;

namespace PriceSync.Modules.TradingRelationships;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapTradingRelationshipsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints
            .MapGroup(Routes.BaseV1)
            .RequireAuthorization();

        api.MapPost("", CreateEndpoint.HandleAsync)
            .WithName(Routes.Names.Create);


        return endpoints;
    }
}
