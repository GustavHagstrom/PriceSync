using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PriceSync.Modules.Agreements.Features.AgreementSelection;

internal sealed record ChangeAgreementSelectionRequest(JsonElement FromVersionId, JsonElement ToVersionId);
internal sealed record AgreementSelectionResponse(Guid? ActiveVersionId, string[] Warnings);
internal sealed record AgreementSelectionHistoryResponse(AgreementSelectionTransitionResponse[] Items, long? NextAfterSequence);
internal sealed record AgreementSelectionTransitionResponse(long Sequence, Guid? FromVersionId,
    Guid? ToVersionId, Guid PersonId, DateTimeOffset DecidedAt);

internal static class AgreementSelectionEndpoints
{
    internal static Task<IResult> GetAsync(Guid relationshipId, [FromQuery] string? asOfUtc,
        [FromQuery] string? throughSequence, AgreementSelectionHandler handler, CancellationToken cancellationToken) =>
        handler.GetAsync(relationshipId, asOfUtc, throughSequence, cancellationToken);

    internal static Task<IResult> GetHistoryAsync(Guid relationshipId, [FromQuery] string? afterSequence,
        [FromQuery] string? pageSize, AgreementSelectionHandler handler, CancellationToken cancellationToken) =>
        handler.GetHistoryAsync(relationshipId, afterSequence, pageSize, cancellationToken);

    internal static Task<IResult> PutAsync(Guid relationshipId, ChangeAgreementSelectionRequest request,
        AgreementSelectionHandler handler, CancellationToken cancellationToken) =>
        handler.ChangeAsync(relationshipId, request, cancellationToken);
}
