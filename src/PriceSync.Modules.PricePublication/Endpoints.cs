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
        group.MapPost("", CreateEndpoint.HandleAsync).WithName("CreatePriceList");
        group.MapGet("/{priceListId:guid}", GetEndpoint.HandleAsync).WithName("GetPriceList");
        return endpoints;
    }
}
