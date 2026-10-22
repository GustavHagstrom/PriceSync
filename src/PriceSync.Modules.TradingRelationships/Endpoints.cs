using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.TradingRelationships.Features.Create;
using PriceSync.Modules.TradingRelationships.Features.End;
using PriceSync.Modules.TradingRelationships.Features.Get;
using PriceSync.Modules.TradingRelationships.Features.ListSupplier;

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
        api.MapPost("{tradingRelationshipId:guid}/end", EndEndpoint.HandleAsync).WithName(Routes.Names.End).WithSummary("Ends an existing trading relationship.");
        api.MapGet("{tradingRelationshipId:guid}", GetEndpoint.HandleAsync).WithName(Routes.Names.Get).WithSummary("Retrieves a trading relationship by its ID.");
        api.MapGet("/supplier", ListSupplierEndpoint.HandleAsync).WithName(Routes.Names.ListSuppliers)
            .WithSummary("Lists all trading relationships where the current organization is the supplier.")
            .WithDescription("Optional status filter: active, ended or all. Omit status to include only active.");

        return endpoints;
    }
}
