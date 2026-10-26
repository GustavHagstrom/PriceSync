using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.Agreements.Features.Create;
using PriceSync.Modules.Agreements.Features.Get;
using PriceSync.Modules.Agreements.Features.List;

namespace PriceSync.Modules.Agreements;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapAgreementsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Agreements").RequireAuthorization();
        group.MapPost("/agreements", CreateEndpoint.HandleAsync).WithName("CreateAgreement");
        group.MapPost("/agreements/{agreementId:guid}/versions",
            (Guid agreementId, Features.PublishVersion.PublishRequest request, Features.PublishVersion.PublishHandler handler, CancellationToken cancellationToken) =>
                handler.HandleAsync(agreementId, request, cancellationToken)).WithName("PublishAgreementVersion");
        group.MapGet("/agreements/{agreementId:guid}", GetEndpoint.HandleAsync).WithName("GetAgreement");
        group.MapGet("/agreements", ListEndpoint.HandleAsync).WithName("ListAgreements");
        return endpoints;
    }
}
