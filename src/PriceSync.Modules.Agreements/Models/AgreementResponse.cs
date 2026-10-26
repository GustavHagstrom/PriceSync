using PriceSync.Modules.Agreements.Domain;

namespace PriceSync.Modules.Agreements.Models;

internal sealed record AgreementResponse(Guid Id, Guid TradingRelationshipId, Guid CreatedByPersonId,
    DateTimeOffset CreatedAt, Guid? LatestVersionId)
{
    internal static AgreementResponse From(PriceAgreement agreement) => new(agreement.Id.Value,
        agreement.TradingRelationshipId.Value, agreement.CreatedByPersonId.Value, agreement.CreatedAt, agreement.LatestVersionId?.Value);
}
