using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.TradingRelationships.Features.Create;
using PriceSync.Modules.TradingRelationships.Features.End;
using PriceSync.Modules.TradingRelationships.Features.Get;

namespace PriceSync.Modules.TradingRelationships;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapTradingRelationshipsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints
            .MapGroup(Routes.BaseV1)
            .WithTags("TradingRelationships")
            .RequireAuthorization();

        api.MapPost("", CreateEndpoint.HandleAsync).WithName(Routes.Names.Create).WithSummary("Creates a new trading relationship.");
        api.MapDelete("{tradingRelationshipId:guid}", EndEndpoint.HandleAsync).WithName(Routes.Names.End).WithSummary("Ends an existing trading relationship.");
        api.MapGet("{tradingRelationshipId:guid}", GetEndpoint.HandleAsync).WithName(Routes.Names.Get).WithSummary("Retrieves a trading relationship by its ID.");

        return endpoints;
    }
}
