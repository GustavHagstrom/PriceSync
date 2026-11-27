using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace PriceSync.Modules.Agreements.Features.AgreementSelection;

internal sealed record ChangeAgreementSelectionRequest(JsonElement FromVersionId, JsonElement ToVersionId);
internal sealed record AgreementSelectionResponse(Guid? ActiveVersionId, string[] Warnings);

internal static class AgreementSelectionEndpoints
{
    internal static Task<IResult> GetAsync(Guid relationshipId, AgreementSelectionHandler handler,
        CancellationToken cancellationToken) => handler.GetAsync(relationshipId, cancellationToken);

    internal static Task<IResult> PutAsync(Guid relationshipId, ChangeAgreementSelectionRequest request,
        AgreementSelectionHandler handler, CancellationToken cancellationToken) =>
        handler.ChangeAsync(relationshipId, request, cancellationToken);
}
