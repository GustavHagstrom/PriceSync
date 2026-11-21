using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.Agreements.Features.Create;
using PriceSync.Modules.Agreements.Features.Get;
using PriceSync.Modules.Agreements.Features.List;
using PriceSync.Modules.Agreements.Features.ListVersions;
using PriceSync.Modules.Agreements.Features.PublishVersion;

namespace PriceSync.Modules.Agreements;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapAgreementsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Agreements").RequireAuthorization();
        group.MapPost("/agreements", CreateEndpoint.HandleAsync).WithName("CreateAgreement");
        group.MapPost("/agreements/{agreementId:guid}/versions", PublishVersionEndpoint.HandleAsync).WithName("PublishAgreementVersion");
        group.MapGet("/agreements/{agreementId:guid}", GetEndpoint.HandleAsync).WithName("GetAgreement");
        group.MapGet("/agreements/{agreementId:guid}/versions/{versionId:guid}",
            (Guid agreementId, Guid versionId, Features.GetVersion.GetVersionHandler handler, CancellationToken cancellationToken) =>
                handler.HandleAsync(agreementId, versionId, cancellationToken)).WithName("GetAgreementVersion");
        group.MapGet("/agreements", ListEndpoint.HandleAsync).WithName("ListAgreements");
        group.MapGet("/agreements/{agreementId:guid}/versions", ListVersionsEndpoint.HandleAsync).WithName("ListAgreementVersions");
        return endpoints;
    }
}
