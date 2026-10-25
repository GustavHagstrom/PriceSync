using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.Agreements.Features.Create;
using PriceSync.Modules.Agreements.Features.Get;

namespace PriceSync.Modules.Agreements;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapAgreementsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Agreements").RequireAuthorization();
        group.MapPost("/trading-relationships/{relationshipId:guid}/agreements", CreateEndpoint.HandleAsync).WithName("CreateAgreement");
        group.MapGet("/agreements/{agreementId:guid}", GetEndpoint.HandleAsync).WithName("GetAgreement");
        return endpoints;
    }
}
