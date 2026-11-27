using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.PricePublication.Features.Create;
using PriceSync.Modules.PricePublication.Features.Get;

namespace PriceSync.Modules.PricePublication;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapPricePublicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/price-lists").WithTags("PricePublication").RequireAuthorization();
        group.MapPost("", CreateEndpoint.HandleAsync)
            .WithName("CreatePriceList")
            .WithSummary("Create a supplier price list")
            .WithDescription("Creates a price list for the acting organization. Use type `general` for a general list or `customerSpecificNet` and supply an active customer organization ID.");
        group.MapGet("/{priceListId:guid}", GetEndpoint.HandleAsync)
            .WithName("GetPriceList")
            .WithSummary("Get a supplier price list")
            .WithDescription("Returns the name, type, currency, market, and customer scope of a price list owned by the acting organization.");
        return endpoints;
    }
}
