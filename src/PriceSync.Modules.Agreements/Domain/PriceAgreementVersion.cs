using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Domain;

internal sealed class PriceAgreementVersion
{
    private PriceAgreementVersion() { }
    public AgreementVersionId Id { get; private set; }
    public AgreementId AgreementId { get; private set; }
    public TradingRelationshipId TradingRelationshipId { get; private set; }
    public AgreementVersionId? PredecessorVersionId { get; private set; }
    public string? NormalizedRequest { get; private set; }
    public long Ordinal { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public PersonId PublishedByPersonId { get; private set; }
    public int DecimalPlaces { get; private set; }
    public string Midpoint { get; private set; } = null!;
    public string RoundingStage { get; private set; } = null!;
    public bool PreservePriceQuantityAndUnitOfMeasure { get; private set; }

    internal static PriceAgreementVersion Create(PriceAgreement agreement, DateOnly validFrom, DateOnly? validTo,
        PersonId person, DateTimeOffset now, AgreementRoundingPolicy rounding, string normalizedRequest) => new()
    {
        Id = AgreementVersionId.Create(), AgreementId = agreement.Id, TradingRelationshipId = agreement.TradingRelationshipId,
        PredecessorVersionId = agreement.LatestVersionId, Ordinal = agreement.LatestOrdinal + 1, ValidFrom = validFrom, ValidTo = validTo, PublishedByPersonId = person,
        PublishedAt = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero),
        DecimalPlaces = rounding.DecimalPlaces, Midpoint = rounding.Midpoint, RoundingStage = rounding.Stage,
        PreservePriceQuantityAndUnitOfMeasure = rounding.PreservePriceQuantityAndUnitOfMeasure,
        NormalizedRequest = normalizedRequest,
    };
}
